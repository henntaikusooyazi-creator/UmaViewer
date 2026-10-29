import struct

import pytest

from pcm_source_domain import compare_observed, exact_sample_preimage


def bits(value):
    return struct.unpack("<I", struct.pack("<f", value))[0]


def test_zero_and_actual_integer_sample_are_exact():
    assert exact_sample_preimage(0, 48000, 100000) == (0, 0)
    for n in (1, 1024, 4096, 725984):
        result = exact_sample_preimage(bits(n/48000), 48000, 1000000)
        assert result == (n, n)


def test_quantization_bucket_can_contain_multiple_integers():
    # One float32 ULP spans more than one sample at this explicit synthetic time.
    lo, hi = exact_sample_preimage(bits(10000.), 192000, 2000000000)
    assert hi-lo > 1
    for n in (lo, hi):
        assert bits(n/192000) == bits(10000.)
    assert bits((lo-1)/192000) != bits(10000.)
    assert bits((hi+1)/192000) != bits(10000.)


def test_outside_clip_or_between_samples_has_no_preimage():
    assert exact_sample_preimage(bits(1.), 48000, 48000) is None
    assert exact_sample_preimage(1, 48000, 100000) is None


def test_observation_equality_never_uses_a_nearest_sample():
    word = bits(1.)
    result = compare_observed(word, 48000, 100000, [47999, 48001])
    assert result["sample_preimage"] == (48000, 48000) and not result["observed_matches"]
    assert compare_observed(word, 48000, 100000, [48000])["observed_matches"] == [0]
    assert compare_observed(1, 48000, 100000, [0])["observed_matches"] == []


@pytest.mark.parametrize("args", [(True, 48000, 100), (-1, 48000, 100), (0x7f800000, 48000, 100),
    (0, 0, 100), (0, True, 100), (0, 48000, 0), (0, 48000, 2**31)])
def test_invalid_float_domains(args):
    with pytest.raises(ValueError):
        exact_sample_preimage(*args)


@pytest.mark.parametrize("positions", [[], (), [-1], [100000], [True], [0]*16385])
def test_invalid_observed_domains(positions):
    with pytest.raises(ValueError):
        compare_observed(0, 48000, 100000, positions)


def test_odd_and_even_float_ties():
    for word in (bits(10000.), bits(10000.)+1, bits(10000.)+2):
        lo, hi = exact_sample_preimage(word, 192000, 2000000000)
        assert bits(lo/192000) == bits(hi/192000) == word
    assert exact_sample_preimage(0x7f7fffff, 48000, 100000) is None


def test_exact_integer_ties_select_the_even_mantissa():
    even = bits(10000.)
    center = 10000*131072
    assert exact_sample_preimage(even, 131072, 2000000000) == (center-64, center+64)
    assert exact_sample_preimage(even+1, 131072, 2000000000) == (center+65, center+191)
