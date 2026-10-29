import pytest
from control_cancel import validate_cancel

RUN = "1" * 32


def request():
    return {"schema": "resident-cancel-before-arm-v1", "run_id": RUN,
            "pid": 42, "birth": 1234, "action": "cancel_before_arm", "runtime_contract": False}


def test_exact_current_owner():
    assert validate_cancel(request(), RUN, 42, 1234)


@pytest.mark.parametrize("field,value", [
    ("schema", "old"), ("run_id", "2" * 32), ("run_id", 1), ("pid", 43),
    ("pid", True), ("birth", 1235), ("birth", True), ("action", "arm"),
    ("runtime_contract", True), ("runtime_contract", 0),
])
def test_foreign_binding_rejected(field, value):
    body = request()
    body[field] = value
    with pytest.raises(ValueError, match="cancel_binding"):
        validate_cancel(body, RUN, 42, 1234)


@pytest.mark.parametrize("value", [None, [], {}, {**request(), "extra": 1}])
def test_wrong_shape_rejected(value):
    with pytest.raises(ValueError, match="cancel_fields"):
        validate_cancel(value, RUN, 42, 1234)


@pytest.mark.parametrize("run_id,pid,birth", [
    (1, 42, 1234), ("A" * 32, 42, 1234), ("1" * 31, 42, 1234),
    (RUN, True, 1234), (RUN, 0, 1234), (RUN, 0x100000000, 1234),
    (RUN, 42, True), (RUN, 42, 0), (RUN, 42, 0x10000000000000000),
])
def test_invalid_owner_rejected(run_id, pid, birth):
    with pytest.raises(ValueError, match="cancel_owner"):
        validate_cancel(request(), run_id, pid, birth)
