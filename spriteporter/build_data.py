import os, json, subprocess, zipfile, shutil
import numpy as np

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
ORIG = os.path.join(RAIZ, 'mitsuo', 'orig')
NXDTOOL = os.path.join(RAIZ, 'tools', 'nxdtool', 'bin', 'Debug', 'net9.0', 'nxdtool.exe')
OUT = os.path.join(AQUI, 'data')
VARIANTES = 8

TABELA = json.load(open(os.path.join(AQUI, 'sprite_table.json')))

UNICOS = {1: 'Ramza (Chapter 1)', 2: 'Ramza (Chapters 2-3)', 3: 'Ramza (Chapter 4)', 4: 'Delita (Chapter 1)',
          5: 'Delita (Chapters 2-3)', 6: 'Delita (Chapter 4)', 7: 'Argath', 8: 'Zalbaag', 9: 'Dycedarg', 10: 'Duke Larg',
          11: 'Duke Goltanna', 12: 'Ovelia', 13: 'Orlandeau', 14: 'High Confessor Funebris', 15: 'Reis (human form)',
          16: 'Zalmour', 17: 'Gaffgarion', 18: 'Marach', 19: 'Simon', 20: 'Alma', 21: 'Orran', 22: 'Mustadio',
          24: 'Cardinal Delacroix', 25: 'Rapha', 27: 'Elmdore', 28: 'Tietra', 29: 'Barrington', 30: 'Agrias',
          31: 'Beowulf', 32: 'Wiegraf (White Knight)', 33: 'Valmafra', 35: 'Ludovich', 36: 'Folmarv', 37: 'Loffrey',
          38: 'Isilud', 39: 'Cletienne', 40: 'Wiegraf (Templar)', 42: 'Meliadoul', 43: 'Barich', 45: 'Celia',
          46: 'Lettie', 50: 'Cloud', 51: 'Zalbaag (undead)'}
JOBS = ['Squire', 'Chemist', 'Knight', 'Archer', 'Monk', 'White Mage', 'Black Mage', 'Time Mage', 'Summoner', 'Thief',
        'Orator', 'Mystic', 'Geomancer', 'Dragoon', 'Samurai', 'Ninja', 'Arithmetician']
GENERICOS = {96 + 2 * i + s: f'{j} ({"male" if s == 0 else "female"})' for i, j in enumerate(JOBS) for s in (0, 1)}
GENERICOS.update({130: 'Bard', 131: 'Dancer', 132: 'Mime (male)', 133: 'Mime (female)'})

def nxd(*args):
    r = subprocess.run([NXDTOOL, *args], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    return r.stdout

def marcador(k):
    return (f'FFTSPRITEPORTER-PALETTE-SLOT-{k}-' * 2).encode()[:48]

def folha(tex):
    g = os.path.join(ORIG, 'g2d')
    a, b = (open(os.path.join(g, f'tex_{tex + i}.bin'), 'rb').read() for i in (0, 1))
    assert len(a) == 131072 and len(b) == 118784, tex
    by = np.frombuffer(a + b, np.uint8); px = np.empty(by.size * 2, np.uint8); px[0::2] = by & 15; px[1::2] = by >> 4
    return px.reshape(976, 512)

if __name__ == '__main__':
    shutil.rmtree(OUT, ignore_errors=True); os.makedirs(OUT)
    chars = []
    for grupo, nomes in (('Characters', UNICOS), ('Generic jobs', GENERICOS)):
        for sid, nome in nomes.items():
            tex = TABELA[sid - 1]
            ids = [i + 1 for i, v in enumerate(TABELA) if v == tex and i + 1 <= 158]
            chars.append({'name': nome, 'group': grupo, 'tex': tex, 'ids': ids})
    assert len({c['tex'] for c in chars}) == len(chars)
    json.dump(chars, open(os.path.join(OUT, 'characters.json'), 'w'), indent=1)
    for c in chars:
        m = (folha(c['tex']) > 0).reshape(244, 4, 128, 4).any(axis=(1, 3))
        open(os.path.join(OUT, f'mask_{c["tex"]}.bin'), 'wb').write(np.packbits(m).tobytes())
    shape0 = os.path.join(ORIG, 'nxd', 'charshape.nxd'); base = open(shape0, 'rb').read()
    tmp = os.path.join(OUT, 'tmp.nxd'); offs = {}
    for sid in sorted({i for c in chars for i in c['ids']}):
        nxd('set', 'CharShape', shape0, tmp, str(sid), 'charclut+Id', '2864434397')
        novo = open(tmp, 'rb').read(); assert len(novo) == len(base)
        dif = [i for i in range(len(base)) if base[i] != novo[i]]
        assert novo[dif[0]:dif[0] + 4] == bytes.fromhex('ddccbbaa') and dif[-1] - dif[0] <= 3, (sid, dif)
        offs[sid] = dif[0]
    json.dump(offs, open(os.path.join(OUT, 'shape.json'), 'w'))
    shutil.copy(shape0, os.path.join(OUT, 'charshape.nxd'))
    clut0 = os.path.join(ORIG, 'nxd', 'charclut.nxd')
    for k in sorted({c['ids'][0] for c in chars}):
        ent = clut0; sai = os.path.join(OUT, f'clut_{k}.nxd')
        for v in range(VARIANTES):
            cores = ','.join(str(x) for x in marcador(v))
            if k <= 3 and v <= 3: nxd('set', 'CharCLUT', ent, tmp, f'{k}.{v}', 'CLUTData', cores, f'{k}.{v}', 'CharaColorSkinId', '0')
            else: nxd('addrow', 'CharCLUT', ent, tmp, '1', '0', str(k), str(v), 'CLUTData', cores, 'CharaColorSkinId', '0')
            os.replace(tmp, sai); ent = sai
        d = open(sai, 'rb').read()
        assert all(d.count(marcador(v)) == 1 for v in range(VARIANTES)), k
    if os.path.exists(tmp): os.remove(tmp)
    with zipfile.ZipFile(os.path.join(AQUI, 'data.zip'), 'w', zipfile.ZIP_DEFLATED) as z:
        for n in sorted(os.listdir(OUT)): z.write(os.path.join(OUT, n), n)
    print(len(chars), 'personagens;', os.path.getsize(os.path.join(AQUI, 'data.zip')), 'bytes em data.zip')
