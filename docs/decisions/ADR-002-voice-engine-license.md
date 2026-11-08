# ADR-002: Offline Speech Synthesis & Zero-Runtime AI Architecture

## Context
Time Chime requires speaking the local time at `:00`, `:15`, `:30`, and `:45` intervals using natural neural voices in English (US/UK) and Hindi. The audio subsystem must:
1. Function 100% offline with zero cloud API dependencies.
2. Execute with near-zero CPU and memory overhead at runtime.
3. Comply cleanly with open-source redistribution licenses (MIT / Apache 2.0).

## Engine & Architecture Evaluation

### 1. Runtime AI Inference vs. Pre-Rendered Finite Audio Bank
Because a wall-clock chime utility only announces time at 15-minute intervals:
- **English (12-hour format without AM/PM)**: Requires exactly $12 \times 4 = 48$ unique spoken phrases per voice (e.g., *"It's nine o'clock."*, *"It's nine fifteen."*).
- **Hindi (Devanagari with time-of-day periods)**: Requires $24 \times 4 = 96$ unique spoken phrases per voice (incorporating *सुबह, दोपहर, शाम, रात*).

Across 6 English voices ($6 \times 48 = 288$) and 2 Hindi voices ($2 \times 96 = 192$), the entire universe of spoken announcements is strictly finite: **480 total recordings**.

### 2. Kokoro-82M v1.0 Neural TTS
- **Model & License**: `hexgrad/Kokoro-82M` v1.0 is released under the permissive **Apache License 2.0**, permitting unrestricted commercial and open-source redistribution of both the model and synthesized audio outputs.
- **Voice Quality**: Delivers natural prosody across American English (`af_heart`, `af_bella`, `am_michael`, `am_eric`), British English (`bf_emma`, `bm_george`), and Hindi (`hf_alpha`, `hm_omega`).

## Decision
1. **Offline Pre-Generation Pipeline (`tools/TimeChime.VoiceGenerator`)**:
   Synthesize all 480 production clips and 32 preview clips ahead of time using Kokoro-82M at 24 kHz, 16-bit mono PCM WAV, normalized to $-1.0\text{ dBFS}$.
2. **Zero AI Runtime in Distributed Client**:
   Do **not** bundle Python, PyTorch, eSpeak-NG, or ONNX Runtime in the end-user application. Play the pre-rendered WAV files directly via Windows native PCM streaming (`SoundPlayer`) coupled with `AudioPcmProcessor` for real-time volume and tempo adjustment.
3. **Permissive Licensing**:
   Distribute application code under the **MIT License** and attribute Kokoro-82M voice assets under **Apache License 2.0**.

## Consequences
- **Positive**: Zero runtime inference latency, zero GPU/CPU spikes, no third-party native DLL crashes, and 100% deterministic audio quality on every Windows PC.
- **Trade-off**: Increases installer/archive size by ~60 MB to store uncompressed 24 kHz WAV assets.
