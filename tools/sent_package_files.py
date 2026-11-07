"""Lists, in the selector entity (Assets/entity/player_model_selector.sent), every file its code loads by path, so the
entity published on its own (a "sent" package, which Sandbox's spawn menu lists) carries them.

Publishing a single asset packs that asset, what it references and the code; the project's Resources are not read.
Two lists in the entity's json, which the entity itself ignores, are picked up as its content compiles:
- PackageAssets: compiled assets the code loads by path (the weapon skin shader), plain paths, so they become the
  entity's references and are packed with it.
- PackageFiles: raw files the code reads (the effects guides' clips), as file references
  ({"$reference_type": "filereference", "path": ...}), which a package carries as they are.

The editor drops both lists if it saves the entity itself (editing its title or description in the inspector): run
this again after that, and whenever a guide clip is added or removed. Usage: sent_package_files.py
"""
import json
import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets')
SENT = os.path.join(ROOT, 'entity', 'player_model_selector.sent')


def files(folder, extension):
    path = os.path.join(ROOT, folder)
    return sorted(f'{folder}/{name}' for name in os.listdir(path) if name.endswith(extension))


assets = ['shaders/weapon_skin.shader']
raw = files('ui/effects/guides', '.webp')

with open(SENT, 'rb') as f:
    text = f.read().decode('utf-8')
newline = '\r\n' if '\r\n' in text else '\n'
entity = json.loads(text)
references = entity.pop('__references', [])
version = entity.pop('__version', 0)
entity['PackageAssets'] = assets
entity['PackageFiles'] = [{'$reference_type': 'filereference', 'path': path} for path in raw]
entity['__references'] = references
entity['__version'] = version
with open(SENT, 'wb') as f:
    f.write(json.dumps(entity, indent=2).replace('\n', newline).encode('utf-8'))
print(f'{len(assets)} assets and {len(raw)} files listed in {os.path.relpath(SENT)}')
