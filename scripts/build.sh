#!/usr/bin/env bash
# Cross-build the Windows app on Linux/macOS (needs .NET SDK 8) and package release assets.
# Usage: scripts/build.sh [version]     -> dist/ZiwuMeridianClock-v<ver>-win-x64.zip, -web.zip, SHA256SUMS.txt
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"; cd "$ROOT"
VER="${1:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' src/ZiwuMeridianClock/ZiwuMeridianClock.csproj | head -1)}"
OUT="$ROOT/dist"; PKG="$OUT/ZiwuMeridianClock"
rm -rf "$OUT"; mkdir -p "$PKG"
dotnet publish src/ZiwuMeridianClock/ZiwuMeridianClock.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableWindowsTargeting=true -p:Version="$VER" -o "$OUT/publish"
cp "$OUT/publish/ZiwuMeridianClock.exe" "$PKG/"
cp -r web "$PKG/web"
cp src/ZiwuMeridianClock/使用说明.txt src/ZiwuMeridianClock/README_Lively.txt LICENSE THIRD_PARTY_NOTICES.md "$PKG/"
mkdir -p "$PKG/licenses" && cp licenses/* "$PKG/licenses/"
zipdir() { # zipdir <zipfile> <dir-in-cwd>
  if command -v zip >/dev/null; then zip -qr -9 "$1" "$2"
  else python3 - "$1" "$2" <<'PY'
import sys,os,zipfile
z=zipfile.ZipFile(sys.argv[1],'w',zipfile.ZIP_DEFLATED,compresslevel=9)
for r,_,fs in os.walk(sys.argv[2]):
    for f in sorted(fs): z.write(os.path.join(r,f))
PY
  fi; }
( cd "$OUT" && zipdir "ZiwuMeridianClock-v$VER-win-x64.zip" ZiwuMeridianClock )
( cd "$PKG" && zipdir "$OUT/ZiwuMeridianClock-v$VER-web.zip" web )
( cd "$OUT" && sha256sum ZiwuMeridianClock-v$VER-*.zip > SHA256SUMS.txt && cat SHA256SUMS.txt )
echo "done: $OUT"
