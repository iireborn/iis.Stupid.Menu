import re, os

BS = chr(92)
QT = chr(34)
NL = chr(10)

CS_STRING = r'"((?:[^"\\]|\\.)*)"'

CAT_RE = re.compile(
    r'new(?:\s+ButtonInfo)?\[\]\s*(?:\{\s*\}?\s*,?)?\s*//\s*(?P<name>[^\[]+?)\s*\[(?P<idx>\d+)\]')
ENTRY_RE = re.compile(r'new ButtonInfo\s*(?:\[\])?\s*\{')


def clean(s):
    if not s:
        return ''
    s = s.replace(BS + QT, QT).replace(BS + 'n', ' ')
    s = re.sub(r'<[^>]*>', '', s)
    for ph in ('{0}', '{1}', '{2}'):
        s = s.replace(ph, '')
    return re.sub(r'\s+', ' ', s).strip()


def strip_block_comments(text):
    """Blank out // and /* */ comments while leaving string literals alone, keeping length."""
    out = []
    i, n, in_str = 0, len(text), False
    while i < n:
        ch = text[i]
        if in_str:
            out.append(ch)
            if ch == BS and i + 1 < n:
                out.append(text[i + 1])
                i += 2
                continue
            if ch == QT:
                in_str = False
            i += 1
            continue
        if ch == QT:
            in_str = True
            out.append(ch)
            i += 1
            continue
        if ch == '/' and i + 1 < n:
            nxt = text[i + 1]
            if nxt == '/':
                j = text.find(NL, i)
                j = n if j < 0 else j
                out.append(' ' * (j - i))
                i = j
                continue
            if nxt == '*':
                j = text.find('*/', i + 2)
                j = n if j < 0 else j + 2
                out.append(' ' * (j - i))
                i = j
                continue
        out.append(ch)
        i += 1
    return ''.join(out)


def entries_from(text):
    out = []
    for m in ENTRY_RE.finditer(text):
        i = m.end() - 1
        depth, j, in_str, esc = 0, i, False, False
        while j < len(text):
            ch = text[j]
            if in_str:
                if esc:
                    esc = False
                elif ch == BS:
                    esc = True
                elif ch == QT:
                    in_str = False
            else:
                if ch == QT:
                    in_str = True
                elif ch == '/' and text[j:j + 2] == '//':
                    nl = text.find(NL, j)
                    j = len(text) if nl < 0 else nl
                    continue
                elif ch == '{':
                    depth += 1
                elif ch == '}':
                    depth -= 1
                    if depth == 0:
                        break
            j += 1
        out.append((m.start(), text[i:j + 1]))
    return out


def parse_body(body):
    name = re.search(r'buttonText\s*=\s*' + CS_STRING, body)
    tip = re.search(r'toolTip\s*=\s*' + CS_STRING, body)
    if not name:
        return None
    return clean(name.group(1)), clean(tip.group(1) if tip else ''), ('isTogglable = false' in body)


def parse_buttons_cs(path):
    text = strip_block_comments(open(path, encoding='utf-8', errors='surrogateescape').read())

    names_block = re.search(r'categoryNames\s*=\s*\{(.*?)\};', text, re.S).group(1)
    names = [clean(m.group(1)) for m in re.finditer(CS_STRING, names_block)]

    end = text.find('public static string[] categoryNames')
    region = text[:end] if end > 0 else text

    markers = [m.start() for m in CAT_RE.finditer(region)]
    cats = {n: [] for n in names}

    for start, body in entries_from(region):
        slot = 0
        for k, mpos in enumerate(markers):
            if mpos < start:
                slot = k
            else:
                break
        if slot < len(names):
            parsed = parse_body(body)
            if parsed:
                cats[names[slot]].append(parsed)
    return names, cats


def parse_other(path):
    text = strip_block_comments(open(path, encoding='utf-8', errors='surrogateescape').read())
    out = []
    for _, body in entries_from(text):
        parsed = parse_body(body)
        if parsed:
            out.append(parsed)
    return out


names, cats = parse_buttons_cs('Menu/Buttons.cs')

extra = []
for root, dirs, files in os.walk('.'):
    dirs[:] = [d for d in dirs if d not in ('.git', 'bin', 'obj', 'References', 'Resources')]
    for fn in sorted(files):
        if not fn.endswith('.cs'):
            continue
        p = os.path.join(root, fn).replace(BS, '/')
        if p.startswith('./'):
            p = p[2:]
        if p == 'Menu/Buttons.cs':
            continue
        flat = parse_other(p)
        if flat:
            extra.append((p, flat))

total_main = sum(len(v) for v in cats.values())
total_extra = sum(len(e[1]) for e in extra)

out = []


def emit(title, tip, action):
    if action:
        out.append('- **%s** — *Action.* %s' % (title, tip) if tip else '- **%s** — *Action.*' % title)
    else:
        out.append('- **%s**%s' % (title, (' — ' + tip) if tip else ''))


out.append('# Feature List')
out.append('')
out.append("Every mod, tool and setting exposed by ii's Stupid Menu, extracted directly from the")
out.append('menu\'s own button definitions — the static tabs in `Menu/Buttons.cs` plus the tabs built')
out.append('at runtime by the managers under `Managers/`, `Mods/` and `Menu/`.')
out.append('')
out.append('- **Menu version:** 1.0.3')
out.append('- **Tabs:** %d' % len(names))
out.append('- **Features declared in the static tabs:** %d' % total_main)
out.append('- **Features built at runtime:** %d' % total_extra)
out.append('- **Total features:** %d' % (total_main + total_extra))
out.append('')
out.append('A feature marked **Action** fires once when clicked; everything else is a toggle. The text')
out.append('after the dash is the in-menu tooltip.')
out.append('')
out.append('## Contents')
out.append('')
for n in names:
    anchor = re.sub(r'[^a-z0-9 -]', '', n.lower()).replace(' ', '-')
    out.append('- [%s](#%s) — %d' % (n, anchor, len(cats.get(n, []))))
out.append('- [Runtime-built features](#runtime-built-features) — %d' % total_extra)
out.append('')
out.append('---')
out.append('')

for n in names:
    items = cats.get(n, [])
    out.append('## %s' % n)
    out.append('')
    if not items:
        out.append('_Built at runtime — see [Runtime-built features](#runtime-built-features)._')
        out.append('')
        continue
    for title, tip, action in items:
        emit(title, tip, action)
    out.append('')

out.append('---')
out.append('')
out.append('## Runtime-built features')
out.append('')
out.append("These are created in code rather than declared in `Buttons.cs`, so their contents follow")
out.append("the game's state — players in the room, installed plugins, presets, macos and the online")
out.append('sound library.')
out.append('')

for p, flat in extra:
    out.append('### %s' % p)
    out.append('')
    for title, tip, action in flat:
        emit(title, tip, action)
    out.append('')

with open('FEATURES.md', 'w', encoding='utf-8', newline=NL) as fh:
    fh.write(NL.join(out))

print('tabs:', len(names), '| static features:', total_main, '| runtime features:', total_extra,
      '| total:', total_main + total_extra)
print('tabs with no static buttons:', [n for n in names if not cats.get(n)])
