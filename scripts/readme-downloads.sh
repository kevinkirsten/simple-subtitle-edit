#!/bin/bash
# Rewrites the download links in README.md (between the downloads:start/end markers, English
# and Portuguese) for a release version, so each link starts the right file directly.
#   scripts/readme-downloads.sh 0.1.0
set -euo pipefail
cd "$(dirname "$0")/.."
v="$1"
base="https://github.com/kevinkirsten/simple-subtitle-edit/releases/download/v$v"

en=$(cat <<MD
| | Download | |
|---|---|---|
| 🍎 **macOS** — Apple Silicon (M1–M5) | [**Simple-Subtitle-Edit-$v-macOS-Apple-Silicon.dmg**]($base/Simple-Subtitle-Edit-$v-macOS-Apple-Silicon.dmg) | Open it, drag the app to Applications |
| 🍎 **macOS** — Intel | [Simple-Subtitle-Edit-$v-macOS-Intel.dmg]($base/Simple-Subtitle-Edit-$v-macOS-Intel.dmg) | Open it, drag the app to Applications |
| 🪟 **Windows** — most PCs | [**Simple-Subtitle-Edit-$v-Windows-x64-Setup.exe**]($base/Simple-Subtitle-Edit-$v-Windows-x64-Setup.exe) | Run it |
| 🪟 **Windows** — ARM (Snapdragon, Surface Pro X) | [Simple-Subtitle-Edit-$v-Windows-arm64-Setup.exe]($base/Simple-Subtitle-Edit-$v-Windows-arm64-Setup.exe) | Run it |
| 🐧 **Ubuntu / Debian** | [simple-subtitle-edit_${v}_amd64.deb]($base/simple-subtitle-edit_${v}_amd64.deb) · [arm64]($base/simple-subtitle-edit_${v}_arm64.deb) | \`sudo apt install ./simple-subtitle-edit_*.deb\` |
| 🐧 **Fedora** | [simple-subtitle-edit-$v-1.x86_64.rpm]($base/simple-subtitle-edit-$v-1.x86_64.rpm) · [aarch64]($base/simple-subtitle-edit-$v-1.aarch64.rpm) | \`sudo dnf install ./simple-subtitle-edit-*.rpm\` |

Version $v · [all files and release notes](https://github.com/kevinkirsten/simple-subtitle-edit/releases/tag/v$v) · Windows without installer: [x64 zip]($base/Simple-Subtitle-Edit-$v-Windows-x64-portable.zip), [ARM zip]($base/Simple-Subtitle-Edit-$v-Windows-arm64-portable.zip)
MD
)

pt=$(cat <<MD
| | Download | |
|---|---|---|
| 🍎 **macOS** — Apple Silicon (M1–M5) | [**Simple-Subtitle-Edit-$v-macOS-Apple-Silicon.dmg**]($base/Simple-Subtitle-Edit-$v-macOS-Apple-Silicon.dmg) | Abra e arraste o app para Applications |
| 🍎 **macOS** — Intel | [Simple-Subtitle-Edit-$v-macOS-Intel.dmg]($base/Simple-Subtitle-Edit-$v-macOS-Intel.dmg) | Abra e arraste o app para Applications |
| 🪟 **Windows** — a maioria dos PCs | [**Simple-Subtitle-Edit-$v-Windows-x64-Setup.exe**]($base/Simple-Subtitle-Edit-$v-Windows-x64-Setup.exe) | Execute |
| 🪟 **Windows** — ARM (Snapdragon, Surface Pro X) | [Simple-Subtitle-Edit-$v-Windows-arm64-Setup.exe]($base/Simple-Subtitle-Edit-$v-Windows-arm64-Setup.exe) | Execute |
| 🐧 **Ubuntu / Debian** | [simple-subtitle-edit_${v}_amd64.deb]($base/simple-subtitle-edit_${v}_amd64.deb) · [arm64]($base/simple-subtitle-edit_${v}_arm64.deb) | \`sudo apt install ./simple-subtitle-edit_*.deb\` |
| 🐧 **Fedora** | [simple-subtitle-edit-$v-1.x86_64.rpm]($base/simple-subtitle-edit-$v-1.x86_64.rpm) · [aarch64]($base/simple-subtitle-edit-$v-1.aarch64.rpm) | \`sudo dnf install ./simple-subtitle-edit-*.rpm\` |

Versão $v · [todos os arquivos e notas da versão](https://github.com/kevinkirsten/simple-subtitle-edit/releases/tag/v$v)
MD
)

python3 - "$en" "$pt" <<'PY'
import re, sys
en, pt = sys.argv[1], sys.argv[2]
s = open("README.md").read()
def put(s, name, body):
    pattern = re.compile(r"(<!-- %s:start -->\n).*?(<!-- %s:end -->)" % (name, name), re.S)
    assert pattern.search(s), "marker %s missing" % name
    return pattern.sub(lambda m: m.group(1) + body + "\n" + m.group(2), s)
s = put(s, "downloads", en)
s = put(s, "downloads-pt", pt)
open("README.md", "w").write(s)
PY
echo "README.md download links set to v$v"
