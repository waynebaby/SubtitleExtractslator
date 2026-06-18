# Note to SO Skill Author

Date: 2026-06-16

## Why this note exists

During the SubtitleExtractslator SO governance upgrade, we reached a successful compiled multi-node workflow, but only after several detours that look avoidable at the SO authoring/runtime layer. This note records those detours and the solution directions that would have reduced them.

## What detour we took

### 1. `so-template.json` was not a real workflow graph

The checked-in workflow template compiled, but it was effectively a single `state` node carrying a long prose description. That meant the skill still had a "run this plan" shape even though the file name suggested a deterministic workflow.

Detour taken:

1. We first corrected governance wording.
2. Then we had to prove that the actual leak was structural, not just textual.
3. We replaced the one-node prose container with an explicit multi-node state machine authored as real SO nodes and transitions.

### 2. The runtime model was richer than the docs exposed

The local docs described conceptual node kinds such as `state`, `condition`, `tool`, `ask`, `wait`, `artifact`, and `memory_write`, but they did not provide the exact workflow JSON contract needed to author those nodes directly.

Detour taken:

1. We reflected `Techne.Loom.Abstractions.dll` to discover runtime types.
2. We inspected `WorkflowInstance`, `StateNode`, `CommandTransition`, `ExpressionTransition`, `TransitionGroup`, and related enums.
3. We serialized synthetic workflow objects to recover the actual JSON shape.

### 3. Raw serializer output was not the same as accepted CLI input

The .NET serializer emitted PascalCase property names and numeric enum values, but the SO CLI input contract accepted lower-camel property names and string enum values.

Examples:

1. Serializer emitted `StartNodeId`; CLI expected `startNodeId`.
2. Serializer emitted `WaitBehavior: 0`; CLI expected `waitBehavior: "blockUntilComplete"`.
3. Serializer emitted `Groups` and `TransitionIds`; CLI expected `groups` and `transitionIds`.

Detour taken:

1. We generated valid objects through the runtime model.
2. We converted the serialized JSON into the actual accepted lower-camel/string-enum input shape.
3. We recompiled the converted workflow until the state machine was accepted.

### 4. We had to discover workflow discriminators and enums indirectly

We needed runtime inspection to discover these values:

1. `$kind` discriminator names: `state`, `command`, `expr`, `tbr`
2. `stepKind` values such as `toolCall`, `mcpCall`, `askUser`, `conditionBranch`, `waitResume`, `memoryRead`, `memoryWrite`, `artifactEmit`
3. valid `status` values such as `drafting`, `readyToStart`, `running`, `waitingExternal`, `failed`, `succeeded`

Detour taken:

1. We reflected enums and polymorphic constants from the runtime assembly.
2. We built a sample workflow by code solely to recover the accepted authoring format.

### 5. `compile` behavior did not match the earlier enhancement assumption

We attempted to use `compile --description-file ... --workflow-file ...`, but the released SO runtime `0.2.77` rejected `--description-file`.

Detour taken:

1. We stopped assuming `compile` could materialize workflow JSON from the plan file.
2. We treated `compile` as a validator for an already-authored workflow file.
3. We authored the full workflow graph ourselves.

### 6. `--guide` depended on a local doc path that was not present

The released `so.dll --guide` attempted to resolve a local guide path under `docs/en/reference/products/so-guide.md`. That file was not present in this repo, so the mandatory guide step failed.

Detour taken:

1. We created a local mirror file at the expected path to satisfy startup-contract lookup.
2. We reran the guide/export flow only after creating that local doc surface.

## What worked

The final successful path was:

1. Prove the checked-in template was only a single prose state.
2. Inspect the actual SO runtime model from `Techne.Loom.Abstractions`.
3. Recover the accepted JSON schema by serializing synthetic workflow instances.
4. Author a real multi-node workflow with explicit states, transition groups, expression transitions, command transitions, and step kinds.
5. Convert the generated JSON to the exact lower-camel and string-enum input contract that `so.dll compile` accepts.
6. Validate the generated candidate first.
7. Promote the validated candidate into the checked-in `so-template.json`.
8. Recompile the repo template successfully.

## Suggested solution

### A. Publish the exact workflow JSON authoring contract

This is the highest-value fix.

Please publish one authoritative reference that includes:

1. exact field names expected by CLI input
2. discriminator names for all node and transition kinds
3. enum serialization format actually accepted by the CLI
4. one minimal valid multi-node example
5. one realistic example with condition, tool, ask, wait, artifact, and memory steps

Without that, authors are forced into assembly reflection or reverse engineering.

### B. Provide a first-party workflow scaffold or export command

Useful options:

1. `so.dll init-workflow`
2. `so.dll export-schema`
3. `so.dll materialize --description-file <plan> --workflow-file <json>`
4. `so.dll normalize --workflow-file <json>`

Any one of those would have removed most of the detour.

### C. Make serializer shape and CLI input shape identical

If runtime serialization emits a workflow snapshot, that same shape should ideally be accepted back by the CLI without casing or enum-format conversion.

At the moment, authoring directly from runtime-serialized JSON is misleading.

### D. Ship guide assets with the runtime or degrade gracefully

`so.dll --guide` should not fail because the caller repo lacks a specific checked-out doc path.

Preferred fixes:

1. bundle guide assets inside the package/runtime
2. fall back to packaged guide assets when local repo docs are missing
3. print a clear remediation message naming the missing guide source contract

### E. Clarify the intended role of `skill-plan.md`

The current ecosystem can be read two ways:

1. `skill-plan.md` is only explanatory design context
2. `skill-plan.md` is intended as compile input that can materialize workflow structure

The released `0.2.77` behavior we observed supports the first interpretation, not the second.

Please make that explicit in docs and examples.

## Specific recommendation for future authors

If the current SO runtime behavior is intentional, the authoring guidance should say this plainly:

1. `skill-plan.md` is a maintainer-facing source of orchestration intent.
2. `so-template.json` must already be a real workflow state machine.
3. `compile` validates and audits the workflow; it does not build the graph from prose.
4. workflow JSON must use the exact lower-camel, discriminator, and string-enum input contract accepted by the CLI.

That one paragraph would have prevented most of the wasted loop.

## End state reached here

The final checked-in workflow for this repo is now:

1. a real multi-node state machine
2. explicit about governance-safe seams
3. free of the prior one-node "run this plan" leak
4. validated successfully through released SO runtime `0.2.77`
