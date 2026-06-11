```mermaid
flowchart TD
    state.adoption_decision["Route Adoption Decision"]
    state.advance_candidate_rank["Advance Candidate Rank"]
    state.apply_mcp_setup["Apply MCP Setup"]
    state.ask_candidate_adoption["Ask Candidate Adoption"]
    state.ask_execution_profile["Ask Execution Profile"]
    state.ask_mcp_setup["Ask MCP Setup Permission"]
    state.batch_apply_deltas["Apply Worker Deltas"]
    state.batch_build_queue["Build Or Reconcile Queue"]
    state.batch_cooldown_check["Check Cooldown Or Resume"]
    state.batch_dispatch_check["Check Pending Batch Work"]
    state.batch_init["Initialize Batch Run"]
    state.batch_inline_worker["Run Inline Worker"]
    state.batch_route_worker["Route Worker Execution"]
    state.batch_subagent_worker["Run Worker Subagent"]
    state.batch_wait_resume["Wait For Resume"]
    state.check_mode["Check Operating Mode"]
    state.cli_download_candidate["Download Candidate Via CLI"]
    state.cli_extract_source["Extract Source Via CLI"]
    state.done["Done"]
    state.downloaded_language_check["Check Downloaded Subtitle Language"]
    state.emit_batch_summary["Emit Batch Summary"]
    state.emit_existing_target["Emit Existing Target Result"]
    state.emit_single_summary["Emit Single-Run Summary"]
    state.group_translation_input["Build Translation Groups"]
    state.local_extract_route["Route Local Extraction"]
    state.mcp_download_candidate["Download Candidate Via MCP"]
    state.mcp_extract_source["Extract Source Via MCP"]
    state.mcp_preflight["Validate MCP Prerequisites"]
    state.mcp_setup_check["Check MCP Setup"]
    state.mcp_translate["Translate Via MCP"]
    state.merge_translation["Merge And Write Final SRT"]
    state.model_translate["Translate Via Model Reasoning"]
    state.mux_check["Check Optional Mux Output"]
    state.mux_output["Mux Subtitle Into Media"]
    state.opensubtitles_candidate_check["Check OpenSubtitles Candidates"]
    state.opensubtitles_download_route["Route Download Execution"]
    state.opensubtitles_search["Search OpenSubtitles"]
    state.opensubtitles_timing_gate["Check Whether Timing Validation Is Required"]
    state.probe_media["Probe Media"]
    state.read_localpaths["Read Local Paths"]
    state.route_request_kind["Route Request Kind"]
    state.route_translation_engine["Route Translation Engine"]
    state.single_check_input_kind["Check Input Kind"]
    state.start["Start"]
    state.subtitle_timing_check["Run Subtitle Timing Check"]
    state.target_track_check["Check Existing Target Subtitle"]
    state.timing_check_result["Evaluate Timing Result"]
    state.update_translation_memory["Update Rolling Translation Memory"]
    state.write_downloaded_output["Accept Downloaded Target Subtitle"]
    state.start -->|Reject Candidate| state.local_extract_route
    state.start -->|Adopt Candidate| state.opensubtitles_download_route
    state.start -->|Advance Candidate Rank| state.opensubtitles_download_route
    state.start -->|Apply MCP Setup| state.single_check_input_kind
    state.start -->|Ask To Adopt Candidate| state.adoption_decision
    state.start -->|Collect Execution Profile| state.route_request_kind
    state.start -->|Ask To Configure MCP| state.apply_mcp_setup
    state.start -->|Apply Worker Deltas| state.batch_cooldown_check
    state.start -->|Build Or Reconcile Queue| state.batch_dispatch_check
    state.start -->|Continue Batch| state.batch_dispatch_check
    state.start -->|Batch Queue Empty| state.emit_batch_summary
    state.start -->|Batch Has Pending Work| state.batch_route_worker
    state.start -->|Initialize Batch Workspace| state.batch_build_queue
    state.start -->|Run Inline Worker| state.batch_apply_deltas
    state.start -->|Delegate Worker Batch| state.batch_apply_deltas
    state.start -->|Use Inline Worker| state.batch_inline_worker
    state.start -->|Use Worker Subagent| state.batch_subagent_worker
    state.start -->|Need Cooldown| state.batch_wait_resume
    state.start -->|Wait For Cooldown Or Resume Signal| state.batch_dispatch_check
    state.start -->|Download Candidate Through CLI| state.opensubtitles_timing_gate
    state.start -->|Extract Source Through CLI| state.group_translation_input
    state.start -->|Download Via CLI| state.cli_download_candidate
    state.start -->|Download Via MCP| state.mcp_download_candidate
    state.start -->|Downloaded Subtitle Already Target Language| state.write_downloaded_output
    state.start -->|Downloaded Subtitle Needs Translation| state.group_translation_input
    state.start -->|Emit Batch Summary| state.done
    state.start -->|Emit Existing Target Result| state.done
    state.start -->|Emit Single Summary| state.done
    state.start -->|Extract Via CLI| state.cli_extract_source
    state.start -->|Extract Via MCP| state.mcp_extract_source
    state.start -->|Build Translation Groups| state.update_translation_memory
    state.start -->|Download Candidate Through MCP| state.opensubtitles_timing_gate
    state.start -->|Extract Source Through MCP| state.group_translation_input
    state.start -->|MCP Needs Setup| state.ask_mcp_setup
    state.start -->|Validate MCP Prerequisites| state.mcp_setup_check
    state.start -->|MCP Ready| state.single_check_input_kind
    state.start -->|Translate Through MCP| state.merge_translation
    state.start -->|Merge Translated Groups| state.mux_check
    state.start -->|Translate Through Model Reasoning| state.merge_translation
    state.start -->|Mux Subtitle Into Media| state.emit_single_summary
    state.start -->|Mux Requested| state.mux_output
    state.start -->|Need Timing Check| state.subtitle_timing_check
    state.start -->|Normalize Request| state.read_localpaths
    state.start -->|Candidates Found| state.ask_candidate_adoption
    state.start -->|No Candidates| state.local_extract_route
    state.start -->|Search OpenSubtitles| state.opensubtitles_candidate_check
    state.start -->|Probe Media| state.target_track_check
    state.start -->|Read Local Paths| state.ask_execution_profile
    state.start -->|Batch Request| state.batch_init
    state.start -->|CLI Mode| state.single_check_input_kind
    state.start -->|MCP Mode| state.mcp_preflight
    state.start -->|Single Request| state.check_mode
    state.start -->|Input Is Media| state.probe_media
    state.start -->|Input Is SRT| state.group_translation_input
    state.start -->|No Mux Requested| state.emit_single_summary
    state.start -->|Skip Timing Check| state.downloaded_language_check
    state.start -->|Run Subtitle Timing Check| state.timing_check_result
    state.start -->|Target Track Exists| state.emit_existing_target
    state.start -->|Target Track Missing| state.opensubtitles_search
    state.start -->|Timing Rejected| state.advance_candidate_rank
    state.start -->|Timing Acceptable| state.downloaded_language_check
    state.start -->|Translate Via MCP| state.mcp_translate
    state.start -->|Translate Via Model| state.model_translate
    state.start -->|Update Rolling Translation Memory| state.route_translation_engine
    state.start -->|Accept Downloaded Target Subtitle| state.emit_single_summary
    style state.ask_execution_profile fill:#fff7ed,stroke:#ea580c,stroke-width:3px
