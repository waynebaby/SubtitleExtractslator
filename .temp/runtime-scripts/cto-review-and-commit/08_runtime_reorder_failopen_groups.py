import json
from pathlib import Path

path = Path(r"e:\code\g\SubtitleExtractslator\.github\skills\subtitle-extractslator\assets\so-workflow\so-template.json")
workflow = json.loads(path.read_text(encoding="utf-8"))
nodes = workflow["nodes"]

# Reorder groups so the first transition is the common/default-true route under current SO semantics.
nodes["state.route_request_kind"]["groups"][0]["transitionIds"] = [
    "transition.route_single_request",
    "transition.route_batch_request",
]
nodes["state.check_mode"]["groups"][0]["transitionIds"] = [
    "transition.route_cli_mode",
    "transition.route_mcp_mode",
]
nodes["state.single_check_input_kind"]["groups"][0]["transitionIds"] = [
    "transition.single_is_srt",
    "transition.single_is_media",
]

# Restore fallback-style guards now that ordering matches current SO runtime behavior.
nodes["transition.route_single_request"]["guardExpression"] = 'context["request_kind"] != "batch"'
nodes["transition.route_cli_mode"]["guardExpression"] = 'context["mode"] != "mcp"'
nodes["transition.single_is_media"]["guardExpression"] = 'context["input_extension"] != ".srt"'

# Remove temporary single-input missing-field transitions from the state group and node map.
single_group = nodes["state.single_check_input_kind"]["groups"][0]["transitionIds"]
nodes["state.single_check_input_kind"]["groups"][0]["transitionIds"] = [
    tid for tid in single_group if tid not in {
        "transition.single_missing_input_path",
        "transition.single_missing_output_path",
        "transition.single_missing_input_extension",
    }
]
for transition_id in [
    "transition.single_missing_input_path",
    "transition.single_missing_output_path",
    "transition.single_missing_input_extension",
]:
    nodes.pop(transition_id, None)

path.write_text(json.dumps(workflow, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("reordered fail-open groups for current SO semantics")
