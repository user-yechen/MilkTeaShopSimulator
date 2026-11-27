import re, os, datetime
p = r'd:\UnityProject\奶茶店\Assets\Scenes\奶茶店.unity'
with open(p, 'rb') as f:
    data = f.read().decode('utf-8', 'replace')

# 打印所有 GameObject 的 m_Name + 它的根节点 Transform fileID
lines = data.split('\n')
for i, line in enumerate(lines):
    if 'm_Name:' in line:
        try:
            name_val = line.split('m_Name:',1)[1].strip()
            unescaped = name_val.encode('ascii','replace').decode('unicode_escape', 'replace')
        except:
            continue
        if any(k in unescaped for k in ['奶茶机','加热','前台','Counter','cup','机','Counter','Tea']):
            print(f'LINE {i+1}: {unescaped!r}')

print()
print('--- PrefabInstance 块里的 AddedGameObjects 和 TransformParent ---')
# 查 PrefabInstance 818021204（奶茶机） 和 706974370（加热机） 周围的块
for fid in ['818021204', '706974370']:
    for m in re.finditer(fr'--- !u!1001 &{fid}\s*\nPrefabInstance:', data):
        start = m.start()
        end = data.find('\n--- !u!', start + 50)
        if end < 0: end = len(data)
        block = data[start:end]
        print(f'=== PrefabInstance {fid} ===')
        print(block[:500])
        print('...')
        # 查它的 GameObject 名字（AddedGameObjects 里的第一个或者下一个 GameObject 块）
        # 找这个 PrefabInstance 的下一个 GameObject 块
        go_idx = data.find('--- !u!1 &', end)
        if go_idx > 0:
            go_block = data[go_idx:data.find('\n--- !u!', go_idx + 50)]
            for gl in go_block.split('\n'):
                if 'm_Name:' in gl:
                    print(f'  Game Object 名字: {gl.strip()}')
                    try:
                        print(f'    unescaped: {gl.split("m_Name:",1)[1].strip().encode("ascii","replace").decode("unicode_escape","replace")!r}')
                    except: pass
        print()
        break
