import json
from pathlib import Path

path = Path(r"e:\code\g\SubtitleExtractslator\.github\skills\subtitle-extractslator\assets\so-workflow\so-template.json")
workflow = json.loads(path.read_text(encoding="utf-8"))
nodes = workflow["nodes"]

# Add execution profile gate state just after read_localpaths in stable order.
if "state.execution_profile_gate" not in nodes:
    new_nodes = {}
    for key, value in nodes.items():
        new_nodes[key] = value
        if key == "state.read_localpaths":
            new_nodes["state.execution_profile_gate"] = {
                "$kind": "state",
                "id": "state.execution_profile_gate",
                "name": "Check Execution Profile",
                "description": "Only ask for execution profile fields when required routing inputs are still missing.",
                "groups": [
                    {
                        "id": "group.execution_profile_gate",
                        "strategy": "firstSuccess",
                        "cancelLosers": True,
                        "transitionIds": [
                            "transition.execution_profile_missing_request_kind",
                            "transition.execution_profile_missing_mode",
                            "transition.execution_profile_missing_target_language",
                            "transition.execution_profile_missing_run_id",
                            "transition.execution_profile_ready",
                        ],
                    }
                ],
                "waitBehavior": "blockUntilComplete",
            }
    nodes = new_nodes
    workflow["nodes"] = nodes

nodes["transition.read_localpaths"]["targetNodeId"] = "state.execution_profile_gate"

execution_profile_transitions = {
    "transition.execution_profile_missing_request_kind": {
        "$kind": "expr",
        "id": "transition.execution_profile_missing_request_kind",
        "name": "Missing Request Kind",
        "description": "Ask for execution profile details when request kind is still missing.",
        "targetNodeId": "state.ask_execution_profile",
        "outputPath": "execution_profile_gate",
        "priority": 100,
        "succeedExpression": "true",
        "guardExpression": "context[\"request_kind\"] == null",
        "stepKind": "conditionBranch",
    },
    "transition.execution_profile_missing_mode": {
        "$kind": "expr",
        "id": "transition.execution_profile_missing_mode",
        "name": "Missing Mode",
        "description": "Ask for execution profile details when operating mode is still missing.",
        "targetNodeId": "state.ask_execution_profile",
        "outputPath": "execution_profile_gate",
        "priority": 95,
        "succeedExpression": "true",
        "guardExpression": "context[\"mode\"] == null",
        "stepKind": "conditionBranch",
    },
    "transition.execution_profile_missing_target_language": {
        "$kind": "expr",
        "id": "transition.execution_profile_missing_target_language",
        "name": "Missing Target Language",
        "description": "Ask for execution profile details when target language is still missing.",
        "targetNodeId": "state.ask_execution_profile",
        "outputPath": "execution_profile_gate",
        "priority": 90,
        "succeedExpression": "true",
        "guardExpression": "context[\"target_language\"] == null",
        "stepKind": "conditionBranch",
    },
    "transition.execution_profile_missing_run_id": {
        "$kind": "expr",
        "id": "transition.execution_profile_missing_run_id",
        "name": "Missing Run Id",
        "description": "Ask for execution profile details when run correlation is still missing.",
        "targetNodeId": "state.ask_execution_profile",
        "outputPath": "execution_profile_gate",
        "priority": 85,
        "succeedExpression": "true",
        "guardExpression": "context[\"run_id\"] == null",
        "stepKind": "conditionBranch",
    },
    "transition.execution_profile_ready": {
        "$kind": "expr",
        "id": "transition.execution_profile_ready",
        "name": "Execution Profile Ready",
        "description": "Continue once the minimum routing profile is already available.",
        "targetNodeId": "state.route_request_kind",
        "outputPath": "execution_profile_gate",
        "priority": 80,
        "succeedExpression": "true",
        "guardExpression": "true",
        "stepKind": "conditionBranch",
    },
}

# Insert transitions in stable order after transition.read_localpaths.
new_nodes = {}
for key, value in workflow["nodes"].items():
    new_nodes[key] = value
    if key == "transition.read_localpaths":
        for transition_id, transition in execution_profile_transitions.items():
            new_nodes[transition_id] = transition
workflow["nodes"] = new_nodes
nodes = workflow["nodes"]

nodes["transition.route_single_request"]["guardExpression"] = 'context["request_kind"] == "single"'
nodes["transition.route_cli_mode"]["guardExpression"] = 'context["mode"] == "cli"'

nodes["transition.apply_mcp_setup"]["designNotes"] = (
    "SubagentCall seam. Outer agent must update or merge the MCP config using dotnet + absolute "
    "SubtitleExtractslator.Cli.dll path, preserve unrelated servers, optionally persist FFMPEG_BIN_DIR, "
    "and return mcp_configured=true on success."
)
nodes["transition.apply_mcp_setup"]["stepKind"] = "subagentCall"

single_input_guard_transitions = {
    "transition.single_missing_input_path": {
        "$kind": "expr",
        "id": "transition.single_missing_input_path",
        "name": "Missing Single Input Path",
        "description": "Return to execution-profile collection when the single-input path is still missing.",
        "targetNodeId": "state.ask_execution_profile",
        "outputPath": "input_route",
        "priority": 110,
        "succeedExpression": "true",
        "guardExpression": "context[\"input_path\"] == null",
        "stepKind": "conditionBranch",
    },
    "transition.single_missing_output_path": {
        "$kind": "expr",
        "id": "transition.single_missing_output_path",
        "name": "Missing Single Output Path",
        "description": "Return to execution-profile collection when the single-output path is still missing.",
        "targetNodeId": "state.ask_execution_profile",
        "outputPath": "input_route",
        "priority": 105,
        "succeedExpression": "true",
        "guardExpression": "context[\"output_path\"] == null",
        "stepKind": "conditionBranch",
    },
    "transition.single_missing_input_extension": {
        "$kind": "expr",
        "id": "transition.single_missing_input_extension",
        "name": "Missing Input Extension",
        "description": "Return to execution-profile collection when input kind cannot yet be determined.",
        "targetNodeId": "state.ask_execution_profile",
        "outputPath": "input_route",
        "priority": 100,
        "succeedExpression": "true",
        "guardExpression": "context[\"input_extension\"] == null",
        "stepKind": "conditionBranch",
    },
}

# Insert before single_is_srt in stable order.
new_nodes = {}
for key, value in workflow["nodes"].items():
    if key == "transition.single_is_srt":
        for transition_id, transition in single_input_guard_transitions.items():
            new_nodes[transition_id] = transition
    new_nodes[key] = value
workflow["nodes"] = new_nodes
nodes = workflow["nodes"]

single_group = nodes["state.single_check_input_kind"]["groups"][0]["transitionIds"]
nodes["state.single_check_input_kind"]["groups"][0]["transitionIds"] = [
    "transition.single_missing_input_path",
    "transition.single_missing_output_path",
    "transition.single_missing_input_extension",
    *[tid for tid in single_group if tid not in {
        "transition.single_missing_input_path",
        "transition.single_missing_output_path",
        "transition.single_missing_input_extension",
    }],
]

# Tighten single input routes to explicit values only.
nodes["transition.single_is_media"]["guardExpression"] = 'context["input_extension"] != null && context["input_extension"] != ".srt"'

path.write_text(json.dumps(workflow, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("patched so-template.json")
