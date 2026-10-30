import io, re, sys

SRC = r'C:\AI\PCL2-main\tools\_pcl_extract\src\PageOtherTest.cs'
OUT = r'C:\AI\PCL2-main\XingYue\Pages\PageOther\PageOtherTestCaveData.vb'

text = io.open(SRC, encoding='utf-8').read()
lines = text.splitlines()

# 定位两个方法的行范围（1-based）
def find_line(pat):
    for i, l in enumerate(lines, 1):
        if re.search(pat, l):
            return i
    raise SystemExit('not found: ' + pat)

start_cave = find_line(r'public static string GetRandomCave\(\)')
end_cave = find_line(r'public static string GetRandomHint\(\)')
start_hint = find_line(r'public static string GetRandomPresetHint\(\)')
end_hint = len(lines)

def slice_text(a, b):
    return '\n'.join(lines[a - 1:b - 1])

cave_src = slice_text(start_cave, end_cave)
hint_src = slice_text(start_hint, end_hint)

def unescape(s):
    """把 C# 字符串字面量内容还原成真实字符"""
    out = []
    i = 0
    n = len(s)
    while i < n:
        c = s[i]
        if c != '\\':
            out.append(c)
            i += 1
            continue
        i += 1
        e = s[i]
        if e == 'n':
            out.append('\n'); i += 1
        elif e == 'r':
            out.append('\r'); i += 1
        elif e == 't':
            out.append('\t'); i += 1
        elif e == '0':
            out.append('\0'); i += 1
        elif e == '"':
            out.append('"'); i += 1
        elif e == '\\':
            out.append('\\'); i += 1
        elif e == 'u':
            out.append(chr(int(s[i + 1:i + 5], 16))); i += 5
        elif e == 'x':
            j = i + 1
            hexs = ''
            while j < n and len(hexs) < 4 and s[j] in '0123456789abcdefABCDEF':
                hexs += s[j]; j += 1
            out.append(chr(int(hexs, 16))); i = j
        else:
            out.append(e); i += 1
    return ''.join(out)

def parse_leaf_arrays(src):
    """找出所有『只包含字符串字面量』的 new string[N] { ... } 块，按出现顺序返回字符串列表"""
    res = []
    for m in re.finditer(r'new string\[\d+\]', src):
        # 找到紧随其后的 {
        j = src.find('{', m.end())
        if j < 0:
            continue
        # 扫描到配对的 }
        depth = 0
        k = j
        items = []
        cur = []
        while k < len(src):
            ch = src[k]
            if ch == '$' and src[k + 1] == '"':
                k += 1
                continue
            if ch == '"':
                # 读一个字符串字面量
                q = k + 1
                buf = []
                while src[q] != '"':
                    if src[q] == '\\':
                        buf.append(src[q]); buf.append(src[q + 1]); q += 2
                    else:
                        buf.append(src[q]); q += 1
                items.append(unescape(''.join(buf)).replace('{ModBase.vbLQ}', '"').replace('{ModBase.vbRQ}', '"'))
                k = q + 1
                continue
            if ch == '{':
                depth += 1
            elif ch == '}':
                depth -= 1
                if depth == 0:
                    break
            elif ch not in ' \t\r\n,':
                # 出现非字面量内容（例如 RandomOne(...)），说明不是叶子数组
                items = None
                break
            k += 1
        if items is not None and len(items) >= 10:
            res.append(items)
    return res

cave_arrays = parse_leaf_arrays(cave_src)
hint_arrays = parse_leaf_arrays(hint_src)

print('cave arrays:', [len(a) for a in cave_arrays])
print('hint arrays:', [len(a) for a in hint_arrays])
if len(cave_arrays) != 8 or len(hint_arrays) != 1:
    raise SystemExit('unexpected array layout')

# 过滤掉含有旧品牌名的留言（用户要求：删掉所有 Plain Craft Launcher / PCL / PCL2）
BAN = ('plain craft launcher', 'pcl', '泡菜龙', '启动器', 'launcher', 'hmcl', 'ppccl',
       '主题', '百宝箱', '千万别点', '主页', '版本设置', '下载页', '设置中', '启动选项',
       '龙猫', '曦辰', 'ltcat')

# 官方原文里那条 base64 彩蛋，换成同名新品牌（仍然是 base64，保留彩蛋玩法）
REPLACE = {'UGxhaW4gQ3JhZnQgTGF1bmNoZXIgMu+8gQ==': 'WGluZ1l1ZSBMYXVuY2hlcu+8gQ=='}

def is_banned(s):
    low = s.lower()
    return any(b in low for b in BAN)

PRESET_HINTS = (
    '苦力怕怕猫，也怕豹猫。',
    '床在下界和末地使用会爆炸！',
    '潜行（Shift）时不会从方块边缘掉下去。',
    '铁桶装水可以灭火，倒在岩浆上会变成黑曜石。',
    '附魔台周围放满书架，可以把附魔等级提升到 30 级。',
    '末影箱里的物品在所有末影箱之间共享。',
    '鞘翅要靠烟花火箭加速，用久了还会磨损。',
    '铁砧可以给工具改名，也能把两件装备的附魔合并。',
    '干海带块是很好的燃料，一块能烧 20 个物品。',
    '给村民安排工作方块，就能决定他们的职业。',
    '用骨粉可以立刻让小麦成熟，但会消耗骨粉。',
    '在下界用床当炸弹，可以安全地挖远古残骸。',
    '水流可以做电梯，底下放灵魂沙和气泡柱就行。',
    '附魔金苹果的效果比普通金苹果强得多。',
    '活塞推不动黑曜石，也推不动带方块的箱子。',
    '沼泽里的史莱姆会掉黏液球，可以做成黏液块。',
    '给猫喂生鱼能驯服它，猫会赶走苦力怕。',
    '雪傀儡经过的地方会留下雪，但在下界会融化受伤。',
)

def apply_filter(name, arr):
    if name == 'PresetHints':
        return list(PRESET_HINTS)
    kept, dropped = [], []
    for s in arr:
        (dropped if is_banned(s) else kept).append(s)
    for s in dropped:
        print('[%s] 删除: %s' % (name, s.replace('\n', '\\n')))
    return [REPLACE.get(s, s).replace('龙腾猫跃', '曦辰') for s in kept]


def vb_literal(s):
    """把字符串转成 VB 字面量；换行拆成 vbCrLf 拼接"""
    parts = []
    for seg in re.split(r'\r\n|\r|\n', s):
        parts.append(seg.replace('"', '""'))
    return ' & vbCrLf & '.join('"%s"' % p for p in parts)

def emit_array(name, arr, doc):
    out = []
    out.append('    ')
    out.append("''' <summary> %s </summary>" % doc)
    out.append('    Public ReadOnly %s As String() = New String() {' % name)
    for i, s in enumerate(arr):
        if i == len(arr) - 1:
            # VB 不允许在数组初始化器的 } 前换行，最后一项与 } 写在同一行
            out.append('        %s}' % vb_literal(s))
        else:
            out.append('        %s,' % vb_literal(s))
    out.append('')
    return '\n'.join(out)

sb = []
sb.append("' 自动生成，请勿手动修改。")
sb.append("' 内容来自官方启动器 2.99.0.1 的 PageOtherTest（回声洞留言 / 你知道吗 预设提示）。")
sb.append("' 生成脚本：tools/_gen-cavedata.py")
sb.append('')
sb.append('Friend Module PageOtherTestData')
sb.append('')
for i, arr in enumerate(cave_arrays[:7]):
    arr = apply_filter('CaveGroup%d' % i, arr)
    sb.append(emit_array('CaveGroup%d' % i, arr, '回声洞留言，第 %d 组（共 %d 条）。' % (i + 1, len(arr))))
riddles = apply_filter('CaveRiddles', cave_arrays[7])
sb.append(emit_array('CaveRiddles', riddles, '回声洞隐藏主题的谜语提示（共 %d 条）。' % len(riddles)))
hints = apply_filter('PresetHints', hint_arrays[0])
sb.append(emit_array('PresetHints', hints, '「你知道吗」预设提示（共 %d 条）。' % len(hints)))
sb.append('End Module')
sb.append('')

io.open(OUT, 'w', encoding='utf-8-sig', newline='\r\n').write('\n'.join(sb))
print('written', OUT)
