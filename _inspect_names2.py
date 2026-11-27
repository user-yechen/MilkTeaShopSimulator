import re
p = r'd:\UnityProject\奶茶店\Assets\Scenes\奶茶店.unity'
with open(p, 'rb') as f:
    data = f.read().decode('utf-8', 'replace')

# PrefabInstance 818021204 的根 Transform fileID 是 818021205
# 在 YAML 里，stripped Transform 块（--- !u!4 &818021205 stripped）后面紧跟对应的 GameObject 块
# 让我直接查 --- !u!4 &818021205 stripped 之后的 --- !u!1 块名字

for transform_fid, label in [('818021205', '奶茶机根'), ('706974371', '加热机根')]:
    stripped = f'--- !u!4 &{transform_fid} stripped'
    pos = data.find(stripped)
    if pos < 0:
        print(f'[{label}] 找不到 stripped Transform 块 {transform_fid}')
        continue
    print(f'=== {label} Transform {transform_fid} (line {data[:pos].count(chr(10))+1}) ===')
    end_of_transform = data.find('\n--- !u!', pos + 10)
    print(data[pos:end_of_transform][:200])
    
    # 找它后面的 GameObject 块
    go_start = data.find('\n--- !u!1 ', end_of_transform)
    if go_start < 0:
        print('  后面没有 GameObject 块！')
        continue
    go_end = data.find('\n--- !u!', go_start + 10)
    go_block = data[go_start:go_end]
    # 找 m_Name
    name_match = re.search(r'm_Name:\s*"([^"]*)"', go_block)
    if name_match:
        raw = name_match.group(1)
        try:
            unescaped = raw.encode('ascii','replace').decode('unicode_escape', 'replace')
        except:
            unescaped = raw
        print(f'  对应 GameObject 名字: {unescaped!r}')
    else:
        print('  没找到 m_Name！块内容:')
        print(go_block[:300])
    print()

# 另外打印所有 --- !u!4 &... stripped 后面紧跟的第一个 GameObject 的 m_Name
print('=== 所有 stripped Transform 对应的 GameObject 名字 ===')
for m in re.finditer(r'--- !u!4 &(\d+) stripped\s*\nTransform:', data):
    tfid = m.group(1)
    pos = m.start()
    end_of_tf = data.find('\n--- !u!', pos + 10)
    go_start = data.find('\n--- !u!1 ', end_of_tf)
    if go_start < 0: continue
    go_end = data.find('\n--- !u!', go_start + 10)
    go_block = data[go_start:go_end]
    nm = re.search(r'm_Name:\s*"([^"]*)"', go_block)
    if nm:
        raw = nm.group(1)
        try: u = raw.encode('ascii','replace').decode('unicode_escape','replace')
        except: u = raw
        print(f'  Transform {tfid:>10s} -> GameObject 名字: {u!r}')
