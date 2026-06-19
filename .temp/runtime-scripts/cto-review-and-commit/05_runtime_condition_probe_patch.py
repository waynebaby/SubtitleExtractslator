import json
from pathlib import Path

path = Path(r"e:\code\g\SubtitleExtractslator\.temp\runtime-scripts\cto-review-and-commit\05_runtime_condition_probe_workflow.json")
workflow = json.loads(path.read_text(encoding="utf-8"))
workflow["nodes"]["transition.single"] = {
    "$kind": "expr",
    "id": "transition.single",
    "name": "Single",
    "description": "Should match request_kind == single.",
    "targetNodeId": "state.done",
    "outputPath": "route",
    "priority": 100,
    "succeedExpression": "true",
    "guardExpression": "context[\"request_kind\"] == \"single\"",
    "stepKind": "conditionBranch"
}
workflow["nodes"]["transition.fallback"] = {
    "$kind": "expr",
    "id": "transition.fallback",
    "name": "Fallback",
    "description": "Catch-all fallback.",
    "targetNodeId": "state.done",
    "outputPath": "route",
    "priority": 90,
    "succeedExpression": "true",
    "guardExpression": "true",
    "stepKind": "conditionBranch"
}
path.write_text(json.dumps(workflow, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("condition probe workflow written")
