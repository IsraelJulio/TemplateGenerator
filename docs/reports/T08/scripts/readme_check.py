import re, sys
p = sys.argv[1]; t = open(p, encoding='utf-8').read().splitlines()
heads = []; sec = None; inblk = None; executar_seen = False; viol = []; order = []
for i, l in enumerate(t, 1):
    if inblk is None and l.startswith('```'):
        inblk = l[3:].strip(); start = i; body = []; continue
    if inblk is not None:
        if l.startswith('```'):
            if inblk == 'bash':
                order.append((start, sec, body))
                if not executar_seen and any('localhost:5100' in b for b in body):
                    viol.append((start, sec))
                if any(b.strip().startswith('dotnet run') for b in body): executar_seen = True
            inblk = None
        else: body.append(l)
        continue
    m = re.match(r'^(#{1,4}) (.*)', l)
    if m: heads.append(m.group(2).strip()); sec = m.group(2).strip()
print('SECOES ##:', [h for h in heads if not h.startswith('#')][:0] or [l[3:] for l in t if l.startswith('## ')])
print('BLOCOS bash na ordem (linha | secao | 1a linha):')
for s, sc, b in order: print(f'  {s:4} | {sc} | {b[0][:90] if b else ""}')
print('VIOLACOES (localhost:5100 antes do bloco dotnet run):', viol or 'nenhuma')
refs = set(re.findall(r'(?:seção|passo) \*\*([^*]+)\*\*', '\n'.join(t)))
hn = {re.sub(r'^\d+\.\s*', '', h).replace('`','') for h in heads}
for r in sorted(refs):
    print(f'  ref **{r}** ->', 'OK' if r.replace('`','') in hn else 'NAO EXISTE')
