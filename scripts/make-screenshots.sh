#!/bin/bash
# Renders the README screenshots (docs/images/simple/*.png) without a screen:
# builds a 90 s demo clip with ffmpeg and renders the window headless.
set -euo pipefail
cd "$(dirname "$0")/.."

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
video="$work/Demo S01E01.mkv"

# Test pattern + a tone that is loud 0-2.5 s of every 6 s, like speech.
ffmpeg -v error -y -f lavfi -i "testsrc2=size=1280x720:rate=24000/1001" \
  -f lavfi -i "sine=frequency=220:sample_rate=48000" \
  -filter_complex "[1:a]volume='if(lt(mod(t,6),2.5),1,0.03)':eval=frame,aformat=channel_layouts=stereo[a]" \
  -map 0:v -map "[a]" -t 90 -c:v libx264 -preset ultrafast -c:a aac "$video"
ffmpeg -v error -y -ss 13 -i "$video" -frames:v 1 "$work/Demo S01E01.frame.png"

# A subtitle that is 1.5 s late, the problem the screenshots show being fixed.
cat > "$work/Demo S01E01.pt-BR.srt" <<'SRT'
1
00:00:01,500 --> 00:00:03,500
Tony, você precisa ver isso.

2
00:00:07,500 --> 00:00:09,500
Agora não, estou ocupado.

3
00:00:13,500 --> 00:00:15,500
É sobre o seu tio.

4
00:00:19,500 --> 00:00:21,500
Fala logo, então.

5
00:00:25,500 --> 00:00:27,500
Ele quer uma reunião.
SRT

SSE_SCREENSHOTS_DIR="$PWD/docs/images/simple" SSE_DEMO_VIDEO="$video" \
  dotnet test tests/UI/UITests.csproj --filter "FullyQualifiedName~SimpleWindowScreenshots"
