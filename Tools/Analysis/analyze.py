"""
동거 뮤즈 · M2 채보 시스템 — 오디오 분석기 (STEP 1~3)

음원 → analysis.json (채보 스펙 §2② 분석 사이드카).
  - 채보 에디터(C#)가 이 파일을 읽어 EASY를 자동 배치한다(STEP 4~5는 C#).
  - Claude가 HARD를 공동 작성할 때도 이 파일을 읽는다(소리를 못 들으니 텍스트로 곡을 파악).
개발할 때만 돈다 — 게임 빌드에는 채보 JSON만 들어간다.

사용:  py analyze.py <음원> <출력.json> [--force]
필요:  librosa (py -m pip install --user librosa), mp3는 ffmpeg/audioread 경유
"""

import hashlib
import json
import os
import sys

import numpy as np
import librosa

VERSION = 1
SR = 22050
HOP = 512

# 온셋 시각 다듬기용 고해상도(≈2.9ms, 창 256). 기본 HOP(≈23ms)로만 잡으면 시작점이 평균 +15ms 늦게 찍혔다(클릭 트랙 실측).
FINE_HOP = 64

# 대역 경계(Hz): low = 킥·베이스, mid, high = 스네어·하이햇·멜로디 상단 (채보 스펙 §4-②)
LOW_MAX_HZ = 200
HIGH_MIN_HZ = 2000

# 박 시각이 고정 BPM 직선에서 이만큼(ms, RMS) 넘게 벗어나면 "곡 중간 BPM 변화 의심" — 변속 곡은 에디터에서 BPM 행을 손으로 추가.
# 국소 템포 추정(tempoDrift)은 고정 템포 곡에서도 6%씩 흔들려서(실측) 보조 기준으로만 쓴다.
BEAT_JITTER_WARN_MS = 20
TEMPO_DRIFT_WARN = 0.10


def log(stage):
    # 에디터(AnalysisRunner)가 표준출력 줄을 그대로 진행 상태로 보여준다.
    print(f"STAGE {stage}", flush=True)


def file_hash(path):
    h = hashlib.sha1()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def ms(seconds):
    return int(round(float(seconds) * 1000))


# ------------------------------------------------------------------ STEP 1 · 비트 그리드

def analyze_tempo(oenv, duration, onset_times):
    """박 추적 → 박 시각들에 직선 회귀로 BPM·위상을 맞춘다.
    beat_track의 BPM은 템포그램 해상도 단위로 거칠어서, 실제 박 시각 전체의 기울기가 더 정확하다."""
    tempo, beats = librosa.beat.beat_track(onset_envelope=oenv, sr=SR, hop_length=HOP, units="time")
    tempo = float(np.atleast_1d(tempo)[0])
    beats = np.asarray(beats, dtype=float)
    warnings = []

    if len(beats) >= 8:
        idx = np.arange(len(beats))
        period, intercept = np.polyfit(idx, beats, 1)
        residual = beats - (intercept + period * idx)
        jitter_ms = float(np.sqrt(np.mean(residual ** 2)) * 1000)
    else:
        period, intercept, jitter_ms = 60.0 / tempo, (beats[0] if len(beats) else 0.0), 0.0
        warnings.append("박 검출 부족 — BPM 신뢰도 낮음")

    bpm = 60.0 / period
    # 정수에 아주 가까우면 정수로(대부분의 곡은 정수 BPM). 위상은 그 BPM으로 다시 맞춘다.
    if abs(bpm - round(bpm)) < 0.15:
        bpm = float(round(bpm))
        period = 60.0 / bpm
        idx = np.arange(len(beats))
        intercept = float(np.mean(beats - idx * period)) if len(beats) else 0.0
    offset = float(intercept % period)

    # 위상 보정: 박 추적 시각은 온셋 검출과 같은 지연을 갖는다. 다듬은 온셋 중 격자 근처(±60ms)에 있는 것들의
    # 격자 대비 오차 중앙값만큼 오프셋을 옮긴다 — 노트를 격자에 붙이므로 이 값이 채보 전체 타이밍을 정한다.
    if len(onset_times):
        n = np.round((onset_times - offset) / period)
        d = onset_times - (offset + n * period)
        near = d[np.abs(d) < 0.06]
        if len(near) >= 4:
            offset = float((offset + np.median(near)) % period)

    # 국소 템포가 흔들리면 변속 의심(테스트 클릭 트랙 120→150처럼).
    local = librosa.feature.tempo(onset_envelope=oenv, sr=SR, hop_length=HOP, aggregate=None)
    local = local[local > 0]
    drift = float(np.std(local) / np.median(local)) if len(local) else 0.0
    if jitter_ms > BEAT_JITTER_WARN_MS or drift > TEMPO_DRIFT_WARN:
        warnings.append(f"템포 흔들림 큼(편차 {drift * 100:.1f}%, 박 오차 {jitter_ms:.0f}ms) — 곡 중간 BPM 변화 의심, BPM 행 수동 추가 필요")

    candidates, confidence = octave_candidates(oenv, bpm)
    if confidence < 0.3:
        warnings.append("2배/절반 BPM과 구분이 애매함 — 후보 BPM 확인")

    grid = []
    t = offset
    bar_beat = 0
    while t <= duration:
        grid.append({"tMs": ms(t), "bar": bar_beat // 4 + 1, "div": bar_beat % 4})
        t += period
        bar_beat += 1

    return {
        "bpm": round(bpm, 3),
        "offsetMs": ms(offset),
        "bpmConfidence": round(confidence, 3),
        "bpmCandidates": candidates,
        "tempoDrift": round(drift, 4),
        "beatJitterMs": round(jitter_ms, 1),
        "beatGrid": grid,
    }, warnings


def octave_candidates(oenv, bpm):
    """bpm·절반·2배 각각에서 온셋 자기상관 세기를 비교 — 스펙 §2의 '2:1 옥타브 의심시 신뢰도 낮음'."""
    ac = librosa.autocorrelate(oenv - oenv.mean())
    frames_per_sec = SR / HOP

    def score(b):
        lag = int(round(60.0 / b * frames_per_sec))
        if lag <= 0 or lag >= len(ac):
            return 0.0
        lo, hi = max(1, lag - 1), min(len(ac), lag + 2)
        return float(np.max(ac[lo:hi]))

    raw = {b: score(b) for b in (bpm / 2, bpm, bpm * 2) if 40 <= b <= 320}
    top = max(raw.values()) if raw else 1.0
    candidates = [{"bpm": round(b, 3), "score": round(s / top, 3) if top > 0 else 0.0} for b, s in sorted(raw.items())]
    # 신뢰도는 2배 템포와만 비교: 주기적인 소리는 절반 템포(2박 간격)에서도 늘 강하게 맞아서 그쪽은 구분 근거가 못 된다.
    # 2배 템포 칸(반 박)에도 소리가 꽉 차 있으면(8분 하이햇 등) 지금 BPM이 실제의 절반일 수 있다.
    main = raw.get(bpm, 0.0)
    double = raw.get(bpm * 2, 0.0)
    confidence = 1.0 - max(0.0, double) / main if main > 0 else 0.0
    return candidates, float(np.clip(confidence, 0.0, 1.0))


# ------------------------------------------------------------------ STEP 2 · 온셋

def refine_times(y, coarse_times):
    """각 온셋을 고해상도 에너지에서 '소리가 실제로 올라가기 시작한 지점'으로 당긴다.
    검출 피크는 소리가 이미 커진 뒤라 늦다 — 피크 직전 40ms 안에서 에너지 상승이 가장 가파른 곳을 시작점으로 본다."""
    rms = librosa.feature.rms(y=y, frame_length=256, hop_length=FINE_HOP, center=True)[0]
    rise = np.diff(rms, prepend=rms[0])
    fps = SR / FINE_HOP
    out = []
    for t in coarse_times:
        c = int(round(t * fps))
        lo, hi = max(0, c - int(0.04 * fps)), min(len(rise), c + 2)
        if hi <= lo:
            out.append(t)
            continue
        k = lo + int(np.argmax(rise[lo:hi]))
        out.append(k / fps)
    return np.asarray(out)


def analyze_onsets(y, oenv):
    frames = librosa.onset.onset_detect(onset_envelope=oenv, sr=SR, hop_length=HOP, units="frames")
    if len(frames) == 0:
        return []

    # 대역별 변화량(스펙트럴 플럭스): 대역마다 평균으로 나눠 서로 비교 가능하게 — 저음은 원래 에너지가 커서 그대로면 늘 low가 이긴다.
    mel = librosa.feature.melspectrogram(y=y, sr=SR, hop_length=HOP, n_mels=64)
    logmel = librosa.power_to_db(mel)
    flux = np.maximum(0.0, np.diff(logmel, axis=1, prepend=logmel[:, :1]))
    freqs = librosa.mel_frequencies(n_mels=64, fmax=SR / 2)
    bands = {
        "low": flux[freqs < LOW_MAX_HZ].sum(axis=0),
        "mid": flux[(freqs >= LOW_MAX_HZ) & (freqs < HIGH_MIN_HZ)].sum(axis=0),
        "high": flux[freqs >= HIGH_MIN_HZ].sum(axis=0),
    }
    bands = {k: v / (v.mean() + 1e-9) for k, v in bands.items()}

    rms_db = librosa.amplitude_to_db(librosa.feature.rms(y=y, hop_length=HOP)[0], ref=np.max)
    peak = float(oenv[frames].max()) or 1.0
    times = refine_times(y, librosa.frames_to_time(frames, sr=SR, hop_length=HOP))

    onsets = []
    for i, f in enumerate(frames):
        window = slice(max(0, f - 1), f + 2)
        band = max(bands, key=lambda k: float(bands[k][window].max()))

        # 지속: 다음 온셋 전까지, 소리가 그 온셋 직후 최대치보다 6dB 떨어지는 지점까지(홀드 판정 재료, 스펙 §4-③).
        end = frames[i + 1] if i + 1 < len(frames) else len(rms_db)
        head = float(rms_db[f:min(end, f + 3)].max()) if f < len(rms_db) else -80.0
        k = f
        while k < end and k < len(rms_db) and rms_db[k] > head - 6:
            k += 1
        sustain = (k - f) * HOP / SR

        onsets.append({
            "tMs": ms(times[i]),
            "strength": round(float(oenv[f]) / peak, 3),
            "band": band,
            "sustainMs": ms(sustain),
        })
    return onsets


# ------------------------------------------------------------------ STEP 3 · 구조

def analyze_segments(y, duration):
    """비슷한 소리끼리 구간을 묶고(라벨 A, B, …), 구간별 평균 음량을 에너지(0~1)로."""
    k = int(np.clip(round(duration / 20), 3, 12))
    # chroma_stft: CQT 기반보다 몇 배 빠르고, 구간 나누기용 대략적인 화성 정보로는 충분하다.
    chroma = librosa.feature.chroma_stft(y=y, sr=SR, hop_length=HOP)
    mfcc = librosa.feature.mfcc(y=y, sr=SR, hop_length=HOP, n_mfcc=13)
    feat = np.vstack([librosa.util.normalize(chroma, axis=0), librosa.util.normalize(mfcc, axis=1)])

    # 1초 단위로 평균 내서 구간 분할(프레임 단위는 너무 잘게 쪼개진다).
    step = int(SR / HOP)
    idx = list(range(0, feat.shape[1], step))
    coarse = librosa.util.sync(feat, idx)
    k = min(k, coarse.shape[1])
    bounds = librosa.segment.agglomerative(coarse, k)
    starts = sorted(set(int(idx[b]) for b in bounds))
    if not starts or starts[0] != 0:
        starts = [0] + starts

    rms = librosa.feature.rms(y=y, hop_length=HOP)[0]
    total_frames = feat.shape[1]
    raw = []
    for i, s in enumerate(starts):
        e = starts[i + 1] if i + 1 < len(starts) else total_frames
        if e <= s:
            continue
        raw.append((s, e, float(rms[s:e].mean()), feat[:, s:e].mean(axis=1)))

    top = max(r[2] for r in raw) or 1.0
    labels, protos = [], []
    for s, e, energy, vec in raw:
        # 앞 구간과 충분히 닮았으면 같은 라벨(반복되는 후렴 등).
        label = None
        for lab, proto in protos:
            sim = float(np.dot(vec, proto) / (np.linalg.norm(vec) * np.linalg.norm(proto) + 1e-9))
            if sim > 0.95:
                label = lab
                break
        if label is None:
            label = chr(ord("A") + len(protos))
            protos.append((label, vec))
        labels.append({
            "startMs": ms(librosa.frames_to_time(s, sr=SR, hop_length=HOP)),
            "endMs": ms(min(duration, librosa.frames_to_time(e, sr=SR, hop_length=HOP))),
            "label": label,
            "energy": round(energy / top, 3),
        })
    return labels


# ------------------------------------------------------------------ main

def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    force = "--force" in sys.argv
    if len(args) != 2:
        print(__doc__)
        sys.exit(2)
    audio, out = args

    digest = file_hash(audio)
    if not force and os.path.exists(out):
        try:
            with open(out, encoding="utf-8") as f:
                old = json.load(f)
            if old.get("audioHash") == digest and old.get("version") == VERSION:
                print("CACHED", flush=True)
                return
        except (OSError, ValueError):
            pass

    log("load")
    y, _ = librosa.load(audio, sr=SR, mono=True)
    duration = len(y) / SR

    oenv = librosa.onset.onset_strength(y=y, sr=SR, hop_length=HOP)

    log("onsets")
    onsets = analyze_onsets(y, oenv)

    log("tempo")
    tempo, warnings = analyze_tempo(oenv, duration, np.array([o["tMs"] / 1000.0 for o in onsets]))

    log("segments")
    segments = analyze_segments(y, duration)

    result = {
        "version": VERSION,
        "audio": os.path.basename(audio),
        "audioHash": digest,
        "durationMs": ms(duration),
        **tempo,
        "warnings": warnings,
        "onsets": onsets,
        "segments": segments,
    }
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    with open(out, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=1)
    log("done")


if __name__ == "__main__":
    main()
