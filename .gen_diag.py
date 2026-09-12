import re, os

BS = chr(92)
CS = r'"((?:[^"\\]|\\.)*)"'
BT = re.compile(r'buttonText\s*=\s*' + CS)


def strip(t):
    return re.sub(r'/\*.*?\*/', lambda m: ' ' * len(m.group(0)), t, flags=re.S)


for root, dirs, files in os.walk('.'):
    dirs[:] = [d for d in dirs if d not in ('.git', 'bin', 'obj', 'References', 'Resources')]
    for fn in sorted(files):
        if not fn.endswith('.cs'):
            continue
        p = os.path.join(root, fn).replace(BS, '/')
        if p.startswith('./'):
            p = p[2:]
        t = open(p, encoding='utf-8', errors='surrogateescape').read()
        raw = len(BT.findall(t))
        st = len(BT.findall(strip(t)))
        if raw and p != 'Menu/Buttons.cs':
            opens = [i for i, l in enumerate(t.split(chr(10)), 1) if '/*' in l]
            print('%-44s raw=%4d kept=%4d removed=%3d blockopen_lines=%s' % (p, raw, st, raw - st, opens[:5]))
