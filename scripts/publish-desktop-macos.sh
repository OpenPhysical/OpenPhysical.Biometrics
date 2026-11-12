#!/usr/bin/env bash
# SPDX-License-Identifier: GPL-2.0-or-later
set -euo pipefail

task_repo_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
task_publish_dir="${task_repo_dir}/artifacts/desktop-publish/osx-arm64"
task_app_directory="${OPENPHYSICAL_DESKTOP_APP_DIRECTORY:-${task_repo_dir}/artifacts/apps}"
task_app_path="${task_app_directory}/OpenPhysical Biometric Studio.app"

if [[ "$(uname -s)" != Darwin ]]; then
    echo "Run this publisher on macOS." >&2
    exit 1
fi
task_candidate=false
task_restore_args=(--nologo)
for task_argument in "$@"; do
    case "${task_argument}" in
        --candidate) task_candidate=true ;;
        --no-restore) task_restore_args+=(--no-restore) ;;
        *) echo "Usage: $0 [--candidate] [--no-restore]." >&2; exit 1 ;;
    esac
done
if [[ "${task_candidate}" == true ]]; then
    task_app_path="${task_app_directory}/OpenPhysical Biometric Studio Preview.app"
    echo "Preview build: capture policy validation is not a release approval." >&2
else
    python3 "${task_repo_dir}/scripts/check-desktop-policy.py"
fi
for task_tool in dotnet python3 otool lipo install_name_tool codesign; do
    if ! command -v "${task_tool}" >/dev/null 2>&1; then
        echo "Required publishing tool is unavailable: ${task_tool}" >&2
        exit 1
    fi
done

# Reusing publish output can retain assemblies from removed dependencies.
rm -rf -- "${task_publish_dir}"
DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1 dotnet publish \
    "${task_repo_dir}/src/OpenPhysical.Biometrics.Desktop/OpenPhysical.Biometrics.Desktop.csproj" \
    --configuration Release --runtime osx-arm64 --self-contained true \
    -p:PublishSingleFile=false -p:PublishTrimmed=false -p:UseAppHost=true \
    --output "${task_publish_dir}" "${task_restore_args[@]}" --disable-build-servers -m:1 -nr:false

python3 - "${task_repo_dir}" "${task_publish_dir}" "${task_app_path}" <<'PY'
import json
import os
from pathlib import Path
import plistlib
import re
import shutil
import subprocess
import sys

repo, published, app = (Path(value).resolve() for value in sys.argv[1:4])
stage = app.with_name(app.stem + '.building.app')
if stage.exists():
    shutil.rmtree(stage)
native = stage / 'Contents' / 'MacOS'
resources = stage / 'Contents' / 'Resources'
shutil.copytree(published, native)
resources.mkdir(parents=True)
executable = native / 'OpenPhysical.Biometrics.Desktop'
executable.chmod(executable.stat().st_mode | 0o111)
candidate = app.name.endswith(' Preview.app')

plist = {
    'CFBundleExecutable': executable.name,
    'CFBundleIdentifier': 'org.openphysical.biometrics.desktop' + ('.preview' if candidate else ''),
    'CFBundleName': app.stem,
    'CFBundleDisplayName': app.stem,
    'CFBundleIconFile': 'Fingerprint.icns',
    'CFBundlePackageType': 'APPL',
    'CFBundleShortVersionString': '3.0.0',
    'CFBundleVersion': '3.0.0',
    'NSHighResolutionCapable': True,
    'NSPrincipalClass': 'NSApplication',
}
with (stage / 'Contents' / 'Info.plist').open('wb') as stream:
    plistlib.dump(plist, stream)
shutil.copy2(repo / 'src' / 'OpenPhysical.Biometrics.Desktop' / 'Assets' / 'Fingerprint.icns', resources / 'Fingerprint.icns')

licenses = resources / 'Licenses'
licenses.mkdir()
shutil.copytree(repo / 'src' / 'OpenPhysical.Biometrics.Desktop' / 'Notices', licenses / 'PCSC-7.0.1')
for name in ['LICENSE', 'README.md', 'THIRD_PARTY_NOTICES.md', 'COMMERCIAL-LICENSING.md']:
    shutil.copy2(repo / name, resources / name)
shutil.copy2(repo / 'docs' / 'DESKTOP.md', resources / 'DESKTOP.md')
validation_record = resources / 'capture-policy-validation.json'
if candidate:
    subprocess.run([sys.executable, str(repo / 'scripts' / 'check-desktop-policy.py'),
                    '--research-record', str(validation_record)], check=True)
else:
    shutil.copy2(repo / 'docs' / 'validation' / 'live-image-policy-v2.json', validation_record)

assets_path = repo / 'artifacts' / 'obj' / 'OpenPhysical.Biometrics.Desktop' / 'project.assets.json'
assets = json.loads(assets_path.read_text())
package_roots = [Path(root) for root in assets['packageFolders']]
packages = {name: value['path'] for name, value in assets['libraries'].items()
            if value['type'] == 'package'}
for framework in assets['project']['frameworks'].values():
    for package in framework.get('downloadDependencies', []):
        version = package['version'].strip('[]').split(',')[0].strip()
        packages[f"{package['name']}/{version}"] = f"{package['name'].lower()}/{version}"

def copy_notices(package, relative):
    source = next((root / relative for root in package_roots if (root / relative).is_dir()), None)
    if source is None:
        sys.exit(f'Cannot locate restored package notices: {package}')
    target = licenses / package.replace('/', '-')
    for item in source.iterdir():
        title = item.name.lower()
        if item.is_dir() and title == 'licenses':
            shutil.copytree(item, target / item.name, dirs_exist_ok=True)
        elif item.is_file() and (
            title.startswith(('license', 'copying', 'notice', 'third-party', 'third_party'))
            or title == 'commercial-licensing.md'
        ):
            target.mkdir(exist_ok=True)
            shutil.copy2(item, target / item.name)

for package, relative in packages.items():
    copy_notices(package, relative)
for algorithm in ['openphysical.biometrics.nbis', 'openphysical.biometrics.nfiq1', 'openphysical.biometrics.nfiq2', 'usbdotnet.libusbnative']:
    if not any(name.lower().startswith(algorithm + '/') for name in packages):
        sys.exit(f'The published dependency graph is missing {algorithm}.')

def run(*arguments):
    return subprocess.check_output(arguments, text=True).strip()

magics = {b'\xfe\xed\xfa\xce', b'\xce\xfa\xed\xfe', b'\xfe\xed\xfa\xcf',
          b'\xcf\xfa\xed\xfe', b'\xca\xfe\xba\xbe', b'\xbe\xba\xfe\xca',
          b'\xca\xfe\xba\xbf', b'\xbf\xba\xfe\xca'}
binaries = []
for item in native.rglob('*'):
    if item.is_file():
        with item.open('rb') as stream:
            if stream.read(4) in magics:
                binaries.append(item)
for required in ['libusb-1.0.dylib', 'libopenphysical_nbis.dylib', 'libopenphysical_nfiq1.dylib',
                 'libopenphysical_nfiq2.dylib', 'libFRFXLL.dylib', 'libopencv_core.410.dylib',
                 'libopencv_imgproc.410.dylib', 'libopencv_ml.410.dylib']:
    if not (native / required).is_file():
        sys.exit(f'Missing required bundled native library: {required}')

def library_id(binary):
    lines = run('otool', '-D', str(binary)).splitlines()
    return lines[1].strip() if len(lines) > 1 else None

def dependencies(binary):
    own_id = library_id(binary)
    for line in run('otool', '-L', str(binary)).splitlines()[1:]:
        value = line.strip().split(' (compatibility version', 1)[0]
        if value and value != own_id:
            yield value

aliases = {}
for binary in binaries:
    subprocess.run(['lipo', '-verify_arch', 'arm64', str(binary)], check=True)
    if len(run('lipo', '-archs', str(binary)).split()) > 1:
        temporary = binary.with_name(binary.name + '.arm64')
        subprocess.run(['lipo', str(binary), '-thin', 'arm64', '-output', str(temporary)], check=True)
        temporary.chmod(binary.stat().st_mode)
        temporary.replace(binary)
    aliases[binary.name] = binary
    own_id = library_id(binary)
    if own_id:
        aliases[Path(own_id).name] = binary
        subprocess.run(['install_name_tool', '-id', '@rpath/' + binary.name, str(binary)], check=True)

for binary in binaries:
    for dependency in list(dependencies(binary)):
        if dependency.startswith(('/usr/lib/', '/System/Library/')):
            continue
        target = aliases.get(Path(dependency).name)
        if target is None:
            sys.exit(f'Unbundled native dependency in {binary.name}: {dependency}')
        replacement = '@loader_path/' + os.path.relpath(target, binary.parent)
        if dependency != replacement:
            subprocess.run(['install_name_tool', '-change', dependency, replacement, str(binary)], check=True)
    load_commands = run('otool', '-l', str(binary))
    paths = re.findall(r'cmd LC_RPATH\s+cmdsize \d+\s+path (.*?) \(offset', load_commands)
    for path in paths:
        if path.startswith('/') and not path.startswith(('/usr/lib/', '/System/Library/')):
            subprocess.run(['install_name_tool', '-delete_rpath', path, str(binary)], check=True)

for binary in binaries:
    for dependency in dependencies(binary):
        if dependency.startswith(('/usr/lib/', '/System/Library/')):
            continue
        if not dependency.startswith('@loader_path/'):
            sys.exit(f'Nonportable dependency in {binary.name}: {dependency}')
        resolved = (binary.parent / dependency.removeprefix('@loader_path/')).resolve()
        if not resolved.is_relative_to(stage) or not resolved.is_file():
            sys.exit(f'Native dependency leaves the app bundle: {dependency}')
for binary in binaries:
    if binary != executable:
        subprocess.run(['codesign', '--force', '--sign', '-', str(binary)], check=True)
for resource in native.rglob('*'):
    if resource.is_file() and resource not in binaries:
        subprocess.run(['codesign', '--force', '--sign', '-', str(resource)], check=True)
subprocess.run(['codesign', '--force', '--sign', '-', str(executable)], check=True)
subprocess.run(['codesign', '--force', '--sign', '-', str(stage)], check=True)
subprocess.run(['codesign', '--verify', '--deep', '--strict', str(stage)], check=True)
if app.exists():
    shutil.rmtree(app)
stage.rename(app)
print(f'Published: {app}')
print(f'Bundled and audited {len(binaries)} arm64 native binaries; local ad-hoc signature verified.')
PY
