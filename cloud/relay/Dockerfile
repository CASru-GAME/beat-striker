# Unity Linux プレイヤー（UnityPlayer.so + ランチャー）向け。素の ubuntu では libicu 等が足りず即死→restart ループになりやすい。
FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        tzdata \
        libc6 \
        libstdc++6 \
        libgcc-s1 \
        libcurl4 \
        libssl3 \
        libicu70 \
        libdbus-1-3 \
        libglib2.0-0 \
        libnss3 \
        libexpat1 \
        zlib1g \
        libx11-6 \
        libxext6 \
        libxrandr2 \
        libxi6 \
        libxcursor1 \
        libxinerama1 \
        libasound2 \
        libpulse0 \
        libdrm2 \
        libgbm1 \
        libegl1 \
        libfontconfig1 \
        libfreetype6 \
        libsdl2-2.0-0 \
        libgtk-3-0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
