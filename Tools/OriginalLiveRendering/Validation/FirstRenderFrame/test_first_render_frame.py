from dataclasses import replace

import pytest
from first_render_frame import Getter, Parameter, select_first_frame


def observations():
    getters = [Getter("run-a", 1, 10, 7, 10, 20, "owner", (0, 0, 0, 0), True, 0),
               Getter("run-a", 2, 11, 7, 30, 40, "owner", (1, 2, 3, 4), True, 0),
               Getter("run-a", 3, 12, 7, 80, 90, "owner", (5, 6, 7, 8), True, 0)]
    parameters = [Parameter("run-a", "parameter-a", 11, 7, 50, 60,
                            "owner", (1, 2, 3, 4), True, 0, True)]
    return getters, parameters


def select(gs, ps):
    return select_first_frame(gs, ps, window_complete=True)


def test_actual_later_parameter_frame():
    gs, ps = observations()
    result = select(gs, ps)
    assert (result.status, result.frame, result.getter_ordinal) == ("Matched", 11, 2)
    assert result.parameter == "parameter-a"


def test_input_order_is_not_frame_selection():
    gs, ps = observations()
    assert select(list(reversed(gs)), ps) == select(gs, ps)


@pytest.mark.parametrize("mutation", [
    {"completed": False}, {"error": 1}, {"end": 50}, {"end": 29},
])
def test_invalid_selected_getter_is_unknown(mutation):
    gs, ps = observations()
    gs[1] = replace(gs[1], **mutation)
    assert select(gs, ps).status == "Unknown"


@pytest.mark.parametrize("mutation", [
    {"tid": 8}, {"owner": "other"}, {"words": (1, 2, 3, 5)},
    {"owner_stable": False}, {"error": 1}, {"completed": False},
    {"end": 49}, {"frame": 9},
])
def test_no_fallback_from_invalid_first_parameter(mutation):
    gs, ps = observations()
    ps = [replace(ps[0], **mutation), replace(ps[0], token="later-good", begin=61, end=70)]
    result = select(gs, ps)
    assert result.status == "Unknown" and result.parameter == "parameter-a"


@pytest.mark.parametrize("mutation", [{}, {"error": 1}, {"completed": False}])
def test_two_same_frame_getters_are_unknown(mutation):
    gs, ps = observations()
    gs.append(replace(gs[1], ordinal=4, **mutation))
    assert select(gs, ps).reason == "same_frame_getter_missing_or_ambiguous"


def test_missing_getter_does_not_choose_nearby():
    gs, ps = observations()
    assert select([gs[0], gs[2]], ps).status == "Unknown"


@pytest.mark.parametrize("delta", [1024, 2048])
def test_no_sample_or_frame_offset(delta):
    gs, ps = observations()
    assert select(gs, [replace(ps[0], frame=11 + delta)]).status == "Unknown"


def test_first_getter_frame_can_be_used_only_if_parameter_is_really_there():
    gs, _ = observations()
    p = Parameter("run-a", "same-frame", 10, 7, 21, 25, "owner", (0, 0, 0, 0), True, 0, True)
    assert select(gs, [p]).status == "Matched"


def test_final_retained_getter_does_not_prove_frame_complete():
    gs, ps = observations()
    assert select(gs[:2], ps).reason == "getter_frame_not_closed"


def test_getter_ordinal_gap_is_not_filled():
    gs, ps = observations()
    gs[1] = replace(gs[1], ordinal=3)
    gs[2] = replace(gs[2], ordinal=4)
    assert select(gs, ps).reason == "getter_ordinal_gap_or_duplicate"


def test_large_ordinal_does_not_allocate_a_large_expected_sequence():
    gs, ps = observations()
    gs[1] = replace(gs[1], ordinal=0xfffffffe)
    gs[2] = replace(gs[2], ordinal=0xffffffff)
    assert select(gs, ps).reason == "getter_ordinal_gap_or_duplicate"


@pytest.mark.parametrize("index,mutation", [
    (0, {"owner": "other-owner"}), (2, {"owner": "other-owner"}),
    (0, {"tid": 8}), (2, {"tid": 8}),
])
def test_first_selected_and_following_getter_keep_same_lifetime(index, mutation):
    gs, ps = observations()
    gs[index] = replace(gs[index], **mutation)
    assert select(gs, ps).reason == "getter_lifetime_changed"


def test_parameter_time_tie_is_unknown():
    gs, ps = observations()
    assert select(gs, ps + [replace(ps[0], token="tie")]).status == "Unknown"


@pytest.mark.parametrize("which", ["missing-first", "duplicate-first", "invalid-first", "no-parameter"])
def test_missing_beginning_is_unknown(which):
    gs, ps = observations()
    if which == "missing-first":
        gs = gs[1:]
    elif which == "duplicate-first":
        gs.append(gs[0])
    elif which == "invalid-first":
        gs[0] = replace(gs[0], completed=False)
    else:
        ps = []
    assert select(gs, ps).status == "Unknown"


def test_window_gate_defaults_to_unknown():
    gs, ps = observations()
    assert select_first_frame(gs, ps).status == "Unknown"


@pytest.mark.parametrize("kind", [Getter, Parameter])
def test_observation_does_not_default_missing_completion_or_error(kind):
    with pytest.raises(TypeError):
        kind("run-a", 1, 10, 7, 10, 20, "owner", (0, 0, 0, 0))


@pytest.mark.parametrize("which", ["scope", "integer", "word", "ordinal", "type", "cap", "gate"])
def test_malformed_or_foreign_inputs_rejected(which):
    gs, ps = observations()
    if which == "scope":
        ps = [replace(ps[0], scope="other-run")]
    elif which == "integer":
        ps = [replace(ps[0], tid=True)]
    elif which == "word":
        ps = [replace(ps[0], words=(1, 2, 3, True))]
    elif which == "ordinal":
        gs[1] = replace(gs[1], ordinal=0)
    elif which == "type":
        ps = [None]
    elif which == "cap":
        ps = ps * 17
    else:
        with pytest.raises(ValueError):
            select_first_frame(gs, ps, window_complete=1)
        return
    with pytest.raises(ValueError):
        select(gs, ps)


@pytest.mark.parametrize("mutation", [
    {"tid": 0}, {"begin": 0}, {"frame": 0x80000000},
    {"completed": 1}, {"scope": ""}, {"scope": None},
    {"owner": ""}, {"owner": None}, {"token": ""}, {"owner_stable": 1},
])
def test_missing_identity_and_typed_state_rejected(mutation):
    gs, ps = observations()
    with pytest.raises(ValueError):
        select(gs, [replace(ps[0], **mutation)])
