"""Validate an explicit, owner-bound cancellation before capture ARM."""
import re


def validate_cancel(value, run_id, pid, birth):
    fields = {"schema", "run_id", "pid", "birth", "action", "runtime_contract"}
    if type(value) is not dict or set(value) != fields:
        raise ValueError("cancel_fields")
    if (type(run_id) is not str or re.fullmatch(r"[0-9a-f]{32}", run_id) is None
            or type(pid) is not int or pid <= 0 or pid > 0xffffffff
            or type(birth) is not int or birth <= 0 or birth > 0xffffffffffffffff):
        raise ValueError("cancel_owner")
    if (value["schema"] != "resident-cancel-before-arm-v1"
            or value["action"] != "cancel_before_arm"
            or type(value["run_id"]) is not str or value["run_id"] != run_id
            or type(value["pid"]) is not int or value["pid"] != pid
            or type(value["birth"]) is not int or value["birth"] != birth
            or value["runtime_contract"] is not False):
        raise ValueError("cancel_binding")
    return True
