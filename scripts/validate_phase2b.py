#!/usr/bin/env python3
import os, re, sys, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
errors = []

# 1) Basic source structural check. This is intentionally conservative; the
# real compiler check is the Release dotnet build that follows in Actions.
def strip_cs(text: str) -> str:
    text = re.sub(r'\$?@?"(?:""|\\.|[^"])*"', '""', text, flags=re.S)
    text = re.sub(r"'(?:\\.|[^'\\])*'", "''", text)
    text = re.sub(r'//.*', '', text)
    text = re.sub(r'/\*.*?\*/', '', text, flags=re.S)
    return text

for path in ROOT.rglob('*.cs'):
    text = strip_cs(path.read_text(encoding='utf-8-sig', errors='ignore'))
    balance = 0
    for ch in text:
        if ch == '{': balance += 1
        elif ch == '}':
            balance -= 1
            if balance < 0:
                errors.append(f'unbalanced braces: {path.relative_to(ROOT)}')
                break
    if balance != 0:
        errors.append(f'unbalanced braces: {path.relative_to(ROOT)} ({balance:+d})')

# 2) Every Program.* table referenced by the imported editor code must exist in
# the portable ProgramData replacement.
all_cs = '\n'.join(p.read_text(encoding='utf-8-sig', errors='ignore') for p in ROOT.rglob('*.cs'))
program_uses = set(re.findall(r'\bProgram\.([A-Za-z_]\w*)', all_cs))
program_data = (ROOT / 'Legacy' / 'ProgramData.cs').read_text(encoding='utf-8-sig')
program_defs = set(re.findall(
    r'\b(?:public|internal|private)\s+static\s+(?:readonly\s+)?[A-Za-z0-9_<>\[\],? ]+\s+([A-Za-z_]\w*)\s*(?:=|;)',
    program_data))
for name in sorted(program_uses - program_defs):
    errors.append('missing portable Program member: ' + name)


# 2b) Keep Windows/native desktop-only dependencies out of the Android port.
forbidden = {
    'CriCpkMaker': 'desktop mixed-mode CpkMaker dependency',
    'XFBIN_LIB': 'desktop XFBIN_LIB dependency',
    'NAudio': 'desktop audio dependency',
    'WindowsAPICodePack': 'Windows shell dependency',
    'NodeNetwork': 'desktop node UI dependency',
}
for token, why in forbidden.items():
    if re.search(r'\b' + re.escape(token) + r'\b', all_cs):
        errors.append(f'forbidden Android dependency {token}: {why}')

# 2c) Character-select icon previews are WPF editor metadata. Android semantic
# compilation must never perform executable-side preview PNG I/O.
charsel_model = (ROOT / 'Legacy' / 'Model' / 'CharacterSelectParamModel.cs').read_text(encoding='utf-8-sig', errors='ignore')
if 'File.ReadAllBytes' in charsel_model and 'charsel_icons' in charsel_model:
    errors.append('legacy character-select preview PNG I/O leaked into Android compiler')

# 3) Validate bundled compiler baselines. A missing entry would only fail at
# runtime on the phone, so fail the CI earlier here.
param_zip = ROOT / 'Assets' / 'Payload' / 'nsc_param_base.zip'
api_zip = ROOT / 'Assets' / 'Payload' / 'moddingapi_payload.zip'
for zpath in (param_zip, api_zip):
    if not zpath.is_file() or zpath.stat().st_size == 0:
        errors.append('missing payload: ' + str(zpath.relative_to(ROOT)))

required_param = {
    'NSC/characode.bin.xfbin','NSC/duelPlayerParam.xfbin','NSC/playerSettingParam.bin.xfbin',
    'NSC/skillCustomizeParam.xfbin','NSC/spSkillCustomizeParam.xfbin','NSC/skillIndexSettingParam.xfbin',
    'NSC/supportSkillRecoverySpeedParam.xfbin','NSC/privateCamera.bin.xfbin','NSC/characterSelectParam.xfbin',
    'NSC/costumeBreakColorParam.xfbin','NSC/costumeParam.bin.xfbin','NSC/player_icon.xfbin',
    'NSC/cmnparam.xfbin','NSC/supportActionParam.xfbin','NSC/awakeAura.xfbin','NSC/appearanceAnm.xfbin',
    'NSC/afterAttachObject.xfbin','NSC/playerDoubleEffectParam.xfbin','NSC/spTypeSupportParam.xfbin',
    'NSC/costumeBreakParam.xfbin','NSC/damageeff.bin.xfbin','NSC/effectprm.bin.xfbin',
    'NSC/damageprm.bin.xfbin','NSC/StageInfo.bin.xfbin','NSC/conditionprm.bin.xfbin',
    'NS4/damageeff.bin.xfbin',
    'NSC/charsel.gfx','NSC/charicon_s.gfx','NSC/select_stage.xfbin','NSC/stagesel.gfx','NSC/stagesel_image.gfx',
    'NSC/Templates/stage_tex.png','NSC/Templates/stage_icon.dds','NSC/DefaultIcons/s_test_charicon_s.xfbin',
    'NSC/Runtime/gametitle.gfx','NSC/Runtime/xcmn_win_roll1.gfx','NSC/Runtime/celshade.tex.xfbin',
    'NSC/Runtime/gauge_p.gfx','NSC/Runtime/freebtl_set.gfx','NSC/Runtime/patchnotes.txt',
}
required_api = {
    'moddingapi/param/NSC/guardEffectParam.xfbin','moddingapi/param/NSC/specialCondParam.xfbin',
    'moddingapi/param/NSC/gudoBallParam.xfbin','moddingapi/param/NSC/conditionprmManager.xfbin',
    'moddingapi/param/NSC/partnerSlotParam.xfbin','moddingapi/param/NSC/susanooCondParam.xfbin',
    'moddingapi/param/NSC/ougiAwakeningParam.xfbin','moddingapi/param/NSC/bgmManagerParam.xfbin',
}

if param_zip.is_file():
    try:
        with zipfile.ZipFile(param_zip) as z:
            bad = z.testzip()
            if bad: errors.append('corrupt nsc_param_base.zip entry: ' + bad)
            names = set(z.namelist())
            for name in sorted(required_param - names): errors.append('parameter baseline missing: ' + name)
            if not any(n.startswith('NSC/Resources/') for n in names):
                errors.append('parameter baseline has no NSC/Resources payload')
            minimum_sizes = {
                'NSC/charsel.gfx': 0x40951,
                'NSC/charicon_s.gfx': 0x1ABB4,
                'NSC/select_stage.xfbin': 0x13F2,
                'NSC/stagesel.gfx': 0x29E23,
                'NSC/stagesel_image.gfx': 0x2661,
            }
            for name, minimum in minimum_sizes.items():
                if name in names and z.getinfo(name).file_size < minimum:
                    errors.append(f'baseline file too short for original 2.1.1.0 offsets: {name} ({z.getinfo(name).file_size} < {minimum})')
    except zipfile.BadZipFile as e:
        errors.append('invalid nsc_param_base.zip: ' + str(e))

if api_zip.is_file():
    try:
        with zipfile.ZipFile(api_zip) as z:
            bad = z.testzip()
            if bad: errors.append('corrupt moddingapi_payload.zip entry: ' + bad)
            names = set(z.namelist())
            for name in sorted(required_api - names): errors.append('ModdingAPI payload missing: ' + name)
    except zipfile.BadZipFile as e:
        errors.append('invalid moddingapi_payload.zip: ' + str(e))

if errors:
    print('Phase 2B static validation FAILED:')
    for e in errors: print(' -', e)
    sys.exit(1)
print('Phase 2B static validation OK')
print(f'  C# files: {sum(1 for _ in ROOT.rglob("*.cs"))}')
print(f'  parameter baseline: {param_zip.stat().st_size:,} bytes')
print(f'  ModdingAPI payload: {api_zip.stat().st_size:,} bytes')
