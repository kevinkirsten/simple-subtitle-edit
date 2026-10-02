#!/bin/bash
# Builds "Simple Subtitle Edit.app" and a .dmg for one architecture, with mpv (from IINA),
# ffmpeg and mkvmerge inside, so nothing else has to be installed.
#
#   installer/simple/macos/make-dmg.sh <arm64|x64> <version> [output-dir]
#
# Unsigned (no Apple Developer ID): the first open needs right-click › Open.
set -euo pipefail
cd "$(dirname "$0")/../../.."

arch="$1"
version="$2"
out="${3:-./dist}"
case "$arch" in
  arm64) runtime=osx-arm64; lipo_arch=arm64; mkv_arch=arm64
         ffmpeg_url=https://www.osxexperts.net/ffmpeg81arm.zip
         ffmpeg_sha=ebb82529562b71170807bbc6b0e7eb4f0b13af8cbb0e085bb9e8f6fe709598ad ;;
  x64)   runtime=osx-x64; lipo_arch=x86_64; mkv_arch=x86_64
         ffmpeg_url=https://www.osxexperts.net/ffmpeg80intel.zip
         ffmpeg_sha=2d24d22db78c87f394a5822867acd5c5dc5e762cd261a44bd26923f3a5af3e07 ;;
  *) echo "arch must be arm64 or x64"; exit 1 ;;
esac
iina_version=v1.4.4
iina_sha=dd0fc0bd4b37fb57a1c8d30d6e3201b3a64bafd29959fe56953964613237beb1
mkv_version=102.0
mkv_build=2

work=$(mktemp -d)
trap 'hdiutil detach "$work/iina" >/dev/null 2>&1 || true; hdiutil detach "$work/mkv" >/dev/null 2>&1 || true; rm -rf "$work"' EXIT
mkdir -p "$out"

echo "== publish ($runtime)"
dotnet publish src/ui/UI.csproj -c Release -r "$runtime" --self-contained true \
  -p:PublishSingleFile=true -p:DebugSymbols=false -p:DebugType=none \
  -p:Version="$version" -p:InformationalVersion="$version" -o "$work/publish" >/dev/null
[ "$(lipo -archs "$work/publish/SubtitleEdit")" = "$lipo_arch" ] || { echo "wrong arch"; exit 1; }

echo "== ffmpeg"
curl -fsSL -o "$work/ffmpeg.zip" "$ffmpeg_url"
echo "$ffmpeg_sha  $work/ffmpeg.zip" | shasum -a 256 -c - >/dev/null
unzip -q -o "$work/ffmpeg.zip" -d "$work/ffmpeg"
ffmpeg_bin=$(find "$work/ffmpeg" -type f -name ffmpeg | head -1)
[ "$(lipo -archs "$ffmpeg_bin")" = "$lipo_arch" ] || { echo "ffmpeg wrong arch"; exit 1; }

echo "== libmpv (IINA $iina_version)"
curl -fsSL -o "$work/iina.dmg" "https://github.com/iina/iina/releases/download/$iina_version/IINA.$iina_version.dmg"
echo "$iina_sha  $work/iina.dmg" | shasum -a 256 -c - >/dev/null
hdiutil attach "$work/iina.dmg" -nobrowse -readonly -mountpoint "$work/iina" >/dev/null

echo "== mkvmerge (MKVToolNix $mkv_version)"
mkv_dmg="MKVToolNix-$mkv_version-$mkv_build-$mkv_arch.dmg"
curl -fsSL -o "$work/mkv.dmg" "https://mkvtoolnix.download/macos/releases/$mkv_version/$mkv_dmg"
expected=$(curl -fsSL "https://mkvtoolnix.download/macos/releases/$mkv_version/$mkv_dmg.sha256" | awk '{print $1}')
echo "$expected  $work/mkv.dmg" | shasum -a 256 -c - >/dev/null
hdiutil attach "$work/mkv.dmg" -nobrowse -readonly -mountpoint "$work/mkv" >/dev/null

echo "== assemble app"
app="$work/Simple Subtitle Edit.app"
cp -R installer/macBundle/SubtitleEdit.app "$app"
find "$app" \( -name ".gitkeep" -o -name ".DS_Store" \) -delete
cp "$work/publish/SubtitleEdit" "$app/Contents/MacOS/"
cp "$work/publish/"*.dylib "$app/Contents/MacOS/" 2>/dev/null || true
chmod +x "$app/Contents/MacOS/SubtitleEdit"
cp "$ffmpeg_bin" "$app/Contents/MacOS/ffmpeg" && chmod +x "$app/Contents/MacOS/ffmpeg"
mkdir -p "$app/Contents/Frameworks"
for dylib in "$work/iina/IINA.app/Contents/Frameworks/"*.dylib; do
  case "$(basename "$dylib")" in libswift_*) continue ;; esac
  cp -L "$dylib" "$app/Contents/Frameworks/"
done
[ -f "$app/Contents/Frameworks/libmpv.2.dylib" ] || { echo "libmpv missing"; exit 1; }
mkdir -p "$app/Contents/MacOS/mkvtoolnix/libs"
cp -L "$work/mkv/MKVToolNix.app/Contents/MacOS/mkvmerge" "$app/Contents/MacOS/mkvtoolnix/"
cp -L "$work/mkv/MKVToolNix.app/Contents/MacOS/libs/libQt6Core.6.dylib" "$app/Contents/MacOS/mkvtoolnix/libs/"
executable="$app/Contents/MacOS/SubtitleEdit"
otool -l "$executable" | grep -q "@executable_path/../Frameworks" || install_name_tool -add_rpath "@executable_path/../Frameworks" "$executable"

# Our own name and id, so it never replaces an installed Subtitle Edit, and it does not
# take over .srt files (rank Alternate, not Owner).
plist="$app/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleName Simple Subtitle Edit" \
  -c "Set :CFBundleDisplayName Simple Subtitle Edit" \
  -c "Set :CFBundleIdentifier io.github.kevinkirsten.simplesubtitleedit" \
  -c "Set :CFBundleVersion $version" \
  -c "Add :CFBundleShortVersionString string $version" "$plist" 2>/dev/null || \
  /usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $version" "$plist"
i=0
while /usr/libexec/PlistBuddy -c "Print :CFBundleDocumentTypes:$i" "$plist" >/dev/null 2>&1; do
  /usr/libexec/PlistBuddy -c "Set :CFBundleDocumentTypes:$i:LSHandlerRank Alternate" "$plist"
  i=$((i + 1))
done

# Ad-hoc signature: required for Apple Silicon to run the binaries; not a Developer ID.
codesign --force --deep --sign - "$app" >/dev/null 2>&1

echo "== self-check"
"$app/Contents/MacOS/mkvtoolnix/mkvmerge" --version
"$app/Contents/MacOS/ffmpeg" -hide_banner -version | head -1

echo "== dmg"
dmg_dir="$work/dmg"
mkdir -p "$dmg_dir"
cp -R "$app" "$dmg_dir/"
cp LICENSE "$dmg_dir/LICENSE.txt"
cp installer/simple/macos/README-first-open.txt "$dmg_dir/"
ln -s /Applications "$dmg_dir/Applications"
label=$([ "$arch" = arm64 ] && echo "Apple-Silicon" || echo "Intel")
dmg="$out/Simple-Subtitle-Edit-$version-macOS-$label.dmg"
sync
for attempt in 1 2 3; do
  hdiutil create -volname "Simple Subtitle Edit" -srcfolder "$dmg_dir" -ov -format UDZO "$dmg" >/dev/null && break
  sleep 5
done
echo "$dmg ($(du -h "$dmg" | cut -f1))"
