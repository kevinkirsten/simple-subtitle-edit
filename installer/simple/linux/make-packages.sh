#!/bin/bash
# Builds .deb, .rpm and .tar.gz from a self-contained linux publish folder.
#   installer/simple/linux/make-packages.sh <x64|arm64> <version> <publish-dir> [output-dir]
# The packages depend on the distro's mpv library, ffmpeg and MKVToolNix, so apt/dnf
# install them together with the app.
set -euo pipefail
cd "$(dirname "$0")/../../.."
arch="$1"; version="$2"; publish="$3"; out="${4:-./dist}"
case "$arch" in
  x64)   deb_arch=amd64; rpm_arch=x86_64 ;;
  arm64) deb_arch=arm64; rpm_arch=aarch64 ;;
  *) echo "arch must be x64 or arm64"; exit 1 ;;
esac
mkdir -p "$out"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

stage() { # $1 root
  install -d "$1/opt/simple-subtitle-edit" "$1/usr/bin" "$1/usr/share/applications" "$1/usr/share/icons/hicolor/256x256/apps"
  cp -R "$publish/." "$1/opt/simple-subtitle-edit/"
  chmod 755 "$1/opt/simple-subtitle-edit/SubtitleEdit"
  ln -sf /opt/simple-subtitle-edit/SubtitleEdit "$1/usr/bin/simple-subtitle-edit"
  install -m 644 installer/simple/linux/simple-subtitle-edit.desktop "$1/usr/share/applications/"
  install -m 644 src/ui/Assets/SE.png "$1/usr/share/icons/hicolor/256x256/apps/simple-subtitle-edit.png"
  cp LICENSE "$1/opt/simple-subtitle-edit/LICENSE"
}

# --- .deb ---------------------------------------------------------------------------------
deb="$work/deb"
stage "$deb"
install -d "$deb/DEBIAN"
size=$(du -sk "$deb/opt" | cut -f1)
cat > "$deb/DEBIAN/control" <<CONTROL
Package: simple-subtitle-edit
Version: $version
Architecture: $deb_arch
Maintainer: Simple Subtitle Edit <https://github.com/kevinkirsten/simple-subtitle-edit>
Installed-Size: $size
Depends: libmpv2 | libmpv1, ffmpeg, mkvtoolnix, libicu74 | libicu76 | libicu72 | libicu70 | libicu67
Section: video
Priority: optional
Homepage: https://github.com/kevinkirsten/simple-subtitle-edit
Description: Fix out-of-sync subtitles
 Line subtitles up with the speech, walk a whole series, find subtitles on
 OpenSubtitles.com and save them next to the video or inside the mkv.
 A fork of Subtitle Edit with a simple window on top.
CONTROL
dpkg-deb --build --root-owner-group "$deb" "$out/simple-subtitle-edit_${version}_${deb_arch}.deb"

# --- .rpm ---------------------------------------------------------------------------------
if command -v rpmbuild >/dev/null; then
  rpmroot="$work/rpm"
  mkdir -p "$rpmroot"/{BUILD,RPMS,SOURCES,SPECS,SRPMS,BUILDROOT}
  stage "$work/rpmstage"
  cat > "$rpmroot/SPECS/simple-subtitle-edit.spec" <<SPEC
Name:           simple-subtitle-edit
Version:        $(echo "$version" | tr '-' '~')
Release:        1
Summary:        Fix out-of-sync subtitles
License:        MIT
URL:            https://github.com/kevinkirsten/simple-subtitle-edit
Requires:       (mpv-libs or libmpv2), (ffmpeg-free or ffmpeg), mkvtoolnix, libicu
AutoReqProv:    no
%global __os_install_post %{nil}
%global debug_package %{nil}

%description
Line subtitles up with the speech, walk a whole series, find subtitles on
OpenSubtitles.com and save them next to the video or inside the mkv.

%install
cp -a $work/rpmstage/. %{buildroot}/

%files
/opt/simple-subtitle-edit
/usr/bin/simple-subtitle-edit
/usr/share/applications/simple-subtitle-edit.desktop
/usr/share/icons/hicolor/256x256/apps/simple-subtitle-edit.png
SPEC
  # No BuildArch in the spec: rpmbuild refuses a foreign BuildArch (aarch64 on an x86_64
  # runner), but --target packages the prebuilt files for it.
  rpmbuild --define "_topdir $rpmroot" --target "$rpm_arch-linux" -bb "$rpmroot/SPECS/simple-subtitle-edit.spec" >/dev/null
  cp "$rpmroot"/RPMS/*/*.rpm "$out/"
fi

# --- .tar.gz ------------------------------------------------------------------------------
tar -C "$publish" -czf "$out/Simple-Subtitle-Edit-$version-Linux-$arch.tar.gz" .
ls -la "$out"
