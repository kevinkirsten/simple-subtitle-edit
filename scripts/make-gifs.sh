#!/bin/bash
# Records the README GIFs (docs/images/simple/*.gif) without a screen: builds a 3-episode demo
# series with ffmpeg, renders the window headless frame by frame, and assembles the GIFs.
set -euo pipefail
cd "$(dirname "$0")/.."

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
series="$work/Demo Show"
mkdir -p "$series/Season 01" "$work/frames"

# Speech-like audio: loud 0-2.5 s of every 6 s.
audio="volume='if(lt(mod(t,6),2.5),1,0.03)':eval=frame,aformat=channel_layouts=stereo"
make_episode() { # $1 episode number, $2 lavfi video source
  ffmpeg -v error -y -f lavfi -i "$2" -f lavfi -i "sine=frequency=$((180 + $1 * 40)):sample_rate=48000" \
    -filter_complex "[1:a]$audio[a]" -map 0:v -map "[a]" -t 40 -c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac \
    "$series/Season 01/Demo Show - S01E0$1.mkv"
  mkdir -p "$work/frames/ep$1"
  ffmpeg -v error -y -i "$series/Season 01/Demo Show - S01E0$1.mkv" -t 20 -vf fps=4 "$work/frames/ep$1/%04d.png"
}
make_episode 1 "testsrc2=size=1280x720:rate=24"
make_episode 2 "smptehdbars=size=1280x720:rate=24"
make_episode 3 "rgbtestsrc=size=1280x720:rate=24"

subtitle() { # $1 seconds of delay, $2..$6 lines
  local delay=$1; shift; local n=1; local t
  for line in "$@"; do
    t=$(( (n - 1) * 6 ))
    printf '%d\n%s --> %s\n%s\n\n' "$n" \
      "$(printf '00:00:%02d,%03d' $((t + 0 + ${delay%.*})) $(( ${delay#*.}00 % 1000 )))" \
      "$(printf '00:00:%02d,%03d' $((t + 2 + ${delay%.*})) $(( ${delay#*.}00 % 1000 )))" "$line"
    n=$((n + 1))
  done
}
# Episode 1: 1.5 s late (the problem). Episodes 2 and 3: in sync. online.srt: in sync.
subtitle 1.5 "Tony, you need to see this." "Not now, I'm busy." "It's about your uncle." "Spit it out, then." "He wants a sit-down." "<i>Tomorrow. At the club.</i>" > "$series/Season 01/Demo Show - S01E01.srt"
subtitle 0.0 "Where were you last night?" "Out. Working." "Working where?" "Does it matter?" "It matters to me." "<i>It always does.</i>" > "$series/Season 01/Demo Show - S01E02.srt"
subtitle 0.0 "Somebody's at the door." "At this hour?" "Go see who it is." "You go." "Fine. I'll go." "<i>Nobody was there.</i>" > "$series/Season 01/Demo Show - S01E03.srt"
subtitle 0.0 "Tony, you need to see this (online)." "Not now, I'm busy." "It's about your uncle." "Spit it out, then." "He wants a sit-down." "<i>Tomorrow. At the club.</i>" > "$work/online.srt"

out="$work/out"
SSE_GIF_DIR="$out" SSE_DEMO_DIR="$work" \
  dotnet test tests/UI/UITests.csproj --filter "FullyQualifiedName~GifScenes"

for scene in sync folder online save; do
  ffmpeg -v error -y -framerate 12 -i "$out/$scene/%04d.png" \
    -vf "scale=960:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff:max_colors=128[p];[b][p]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle" \
    "docs/images/simple/$scene.gif"
  echo "docs/images/simple/$scene.gif: $(du -h "docs/images/simple/$scene.gif" | cut -f1)"
done
