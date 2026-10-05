import os, sys, json, zipfile

VERSAO_MOD = '1.2.0'
VERSAO_FERRAMENTA = '1.1.0'
AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(AQUI)
SAIDA = os.path.join(RAIZ, 'compartilhar')
MOD_ID = 'fftivc.utility.spriteporter'
MOD_BIN = os.path.join(RAIZ, 'spriteporter-mod', 'bin', 'Release', 'net9.0-windows')

def confere(caminho):
    arq = zipfile.ZipFile(caminho)
    assert arq.testzip() is None
    print(os.path.basename(caminho), os.path.getsize(caminho), 'bytes,', len(arq.infolist()), 'entradas')

os.makedirs(SAIDA, exist_ok=True)

chars = json.load(open(os.path.join(AQUI, 'data', 'characters.json')))
pastas = [('Job - ' if c['group'] == 'Generic jobs' else '') + c['name'] for c in chars] + ['Ramza (all chapters)', 'Delita (all chapters)']
destino = os.path.join(SAIDA, f'FFT-Sprite-Porter-{VERSAO_MOD}.zip')
with zipfile.ZipFile(destino, 'w', zipfile.ZIP_DEFLATED) as arq:
    for pasta, _, nomes in os.walk(MOD_BIN):
        for n in nomes:
            cheio = os.path.join(pasta, n)
            arq.write(cheio, f'{MOD_ID}/' + os.path.relpath(cheio, MOD_BIN).replace(os.sep, '/'))
    arq.write(os.path.join(RAIZ, 'spriteporter-mod', 'TUTORIAL.txt'), f'{MOD_ID}/TUTORIAL.txt')
    for p in sorted(pastas):
        arq.writestr(zipfile.ZipInfo(f'{MOD_ID}/Sprites/{p}/'), b'')
confere(destino)

if '--maker' in sys.argv:
    destino = os.path.join(SAIDA, f'FFT-Sprite-Porter-Mod-Maker-{VERSAO_FERRAMENTA}.zip')
    with zipfile.ZipFile(destino, 'w', zipfile.ZIP_DEFLATED) as arq:
        for n in ('QUICK START.txt', 'README.txt'):
            arq.write(os.path.join(AQUI, n), f'FFT Sprite Porter Mod Maker/{n}')
        arq.write(os.path.join(AQUI, 'publish', 'FFTSpritePorter.exe'), 'FFT Sprite Porter Mod Maker/FFTSpritePorter.exe')
    confere(destino)
