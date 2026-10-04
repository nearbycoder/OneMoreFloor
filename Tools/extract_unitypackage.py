#!/usr/bin/env python3
"""Extracts a .unitypackage (tar.gz of <guid>/{asset,asset.meta,pathname}) into a project, keeping GUIDs.
Used for TextMeshPro's essential resources, which Unity only imports asynchronously in batch mode.
   python3 Tools/extract_unitypackage.py <package> <projectRoot>"""
import os, sys, tarfile

pkg, root = sys.argv[1], sys.argv[2]
entries = {}
with tarfile.open(pkg, "r:gz") as tar:
    for m in tar.getmembers():
        parts = m.name.strip("./").split("/")
        if len(parts) != 2 or not m.isfile():
            continue
        guid, kind = parts
        entries.setdefault(guid, {})[kind] = tar.extractfile(m).read()
count = 0
for guid, e in entries.items():
    if "pathname" not in e:
        continue
    path = e["pathname"].decode().splitlines()[0].strip()
    dest = os.path.join(root, path)
    if "asset" in e:
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        with open(dest, "wb") as f:
            f.write(e["asset"])
        count += 1
    else:
        os.makedirs(dest, exist_ok=True)
    if "asset.meta" in e:
        with open(dest + ".meta", "wb") as f:
            f.write(e["asset.meta"])
print(f"extracted {count} files from {os.path.basename(pkg)}")
