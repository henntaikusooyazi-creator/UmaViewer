"""Join explicitly supplied observations; never choose a nearby frame or clock."""
from dataclasses import dataclass


@dataclass(frozen=True)
class Getter:
    scope: str
    ordinal: int
    frame: int
    tid: int
    begin: int
    end: int
    owner: str
    words: tuple[int, int, int, int]
    completed: bool
    error: int


@dataclass(frozen=True)
class Parameter:
    scope: str
    token: str
    frame: int
    tid: int
    begin: int
    end: int
    owner: str
    words: tuple[int, int, int, int]
    completed: bool
    error: int
    owner_stable: bool


@dataclass(frozen=True)
class Selection:
    status: str
    reason: str
    frame: int | None = None
    parameter: str | None = None
    getter_ordinal: int | None = None


def _validate(row):
    for name in ("frame", "tid", "begin", "end", "error"):
        value = getattr(row, name)
        if type(value) is not int or not 0 <= value <= 0xffffffffffffffff:
            raise ValueError("invalid_integer")
    if not row.tid or not row.begin or row.frame > 0x7fffffff:
        raise ValueError("invalid_identity")
    if type(row.completed) is not bool or type(row.scope) is not str or not row.scope:
        raise ValueError("invalid_scope_or_completion")
    if type(row.owner) is not str or not row.owner:
        raise ValueError("missing_owner_token")
    if (type(row.words) is not tuple or len(row.words) != 4
            or any(type(w) is not int or not 0 <= w <= 0xffffffff for w in row.words)):
        raise ValueError("invalid_owner_words")
    if isinstance(row, Getter):
        if type(row.ordinal) is not int or not 1 <= row.ordinal <= 0xffffffff:
            raise ValueError("invalid_ordinal")
    elif type(row.token) is not str or not row.token or type(row.owner_stable) is not bool:
        raise ValueError("invalid_parameter_token_or_stability")


def select_first_frame(getters, parameters, *, window_complete=False):
    """The caller qualifies capture coverage and computes getter owner words.

    A match establishes this bounded join only, not command generation, GPU
    identity, a source-to-Viewer epoch contract, or original image equality.
    """
    if type(window_complete) is not bool:
        raise ValueError("invalid_window_gate")
    if len(getters) > 16 or len(parameters) > 16:
        raise ValueError("observation_cap")
    for rows, expected in ((getters, Getter), (parameters, Parameter)):
        for row in rows:
            if type(row) is not expected:
                raise ValueError("invalid_observation_type")
            _validate(row)
    if not window_complete:
        return Selection("Unknown", "incomplete_observation_window")
    first = [g for g in getters if g.ordinal == 1]
    if len(first) != 1:
        return Selection("Unknown", "first_getter_missing_or_ambiguous")
    first = first[0]
    if any(r.scope != first.scope for r in (*getters, *parameters)):
        raise ValueError("foreign_observation_scope")
    if not first.completed or first.error or first.end <= first.begin:
        return Selection("Unknown", "invalid_first_getter")
    later = [p for p in parameters if p.begin > first.end]
    if not later:
        return Selection("Unknown", "first_parameter_missing")
    tick = min(p.begin for p in later)
    earliest = [p for p in later if p.begin == tick]
    if len(earliest) != 1:
        return Selection("Unknown", "first_parameter_order_ambiguous")
    p = earliest[0]

    def unknown(reason):
        return Selection("Unknown", reason, p.frame, p.token)

    if p.frame < first.frame or not p.completed or p.error or p.end <= p.begin:
        return unknown("invalid_first_parameter")
    candidates = [g for g in getters if g.frame == p.frame]
    if len(candidates) != 1:
        return unknown("same_frame_getter_missing_or_ambiguous")
    g = candidates[0]
    if not g.completed or g.error or not g.begin < g.end < p.begin:
        return unknown("getter_not_completed_before_parameter")
    if g.tid != p.tid or g.owner != p.owner or not p.owner_stable or g.words != p.words:
        return unknown("owner_join_mismatch")
    # Close the selected getter frame with an observed next ordinal. A last
    # retained row alone cannot prove another same-frame getter was not lost.
    following = [r for r in getters if r.ordinal == g.ordinal + 1]
    if (len(following) != 1 or not following[0].completed or following[0].error
            or following[0].frame <= p.frame or following[0].begin <= p.end):
        return unknown("getter_frame_not_closed")
    if any(r.owner != g.owner or r.tid != g.tid for r in (first, following[0])):
        return unknown("getter_lifetime_changed")
    ordinals = [r.ordinal for r in getters if r.ordinal <= g.ordinal + 1]
    if (len(ordinals) != g.ordinal + 1
            or sorted(ordinals) != list(range(1, len(ordinals) + 1))):
        return unknown("getter_ordinal_gap_or_duplicate")
    return Selection("Matched", "exact_first_parameter_frame", p.frame, p.token, g.ordinal)
