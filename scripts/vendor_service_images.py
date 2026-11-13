#!/usr/bin/env python3
"""Import the selected fixed photographs into Vite public assets.
Run only after verifying image licenses for redistribution.
"""
from pathlib import Path
from urllib.request import Request, urlopen
import hashlib, json

DEST=Path(__file__).resolve().parents[1]/"src/atwash-web/public/images"
IMAGES={
 "aspiracao.jpg":"https://lirp.cdn-website.com/2d8ab408/dms3rep/multi/opt/Full%2BCar%2BDeep%2BClean-640w.png",
 "higienizacao-interna.jpg":"https://content.app-sources.com/s/970591715528428101/thumbnails/640x480/Images/Full_Interior_Image-6264845.png?format=webp",
 "motor.jpg":"https://img.grouponcdn.com/metro_draft_service/2UpESm9CXpMP88Eg4z9NbD4Byoas/2U-1158x1154/v1/t2800x1696.jpg",
 "ar-condicionado.jpg":"https://i.shgcdn.com/980297aa-55dd-4d71-839d-4b3e97cc157b/-/format/auto/-/preview/3000x3000/-/quality/lighter/",
 "polimento.jpg":"https://cdn.prod.website-files.com/68a506949befd25caf9ad118/6985ac3dc68523b77af916bd_car-wash-carlisle-pa-car-detailing.jpeg",
 "enceramento.jpg":"https://www.nextgenautocare.co.in/assets/images/services/17.jpg",
 "vitrificacao.jpg":"https://images.unsplash.com/photo-1607860108855-64acf2078ed9?auto=format&fit=crop&w=700&q=80",
 "plasticos.jpg":"https://media.karousell.com/media/photos/products/2024/9/13/interior_detailing_tier_2__sha_1726223090_6c3bbcc7_progressive.jpg",
 "lavagem-completa.jpg":"https://www.nextgenautocare.co.in/assets/images/services/17.jpg",
 "lavagem-simples.jpg":"https://images.unsplash.com/photo-1520340356584-f9917d1eea6f?auto=format&fit=crop&w=700&q=80",
 "sidebar-carro.jpg":"https://images.unsplash.com/photo-1493238792000-8113da705763?auto=format&fit=crop&w=650&q=78",
}
def main():
 DEST.mkdir(parents=True,exist_ok=True)
 manifest={}
 for name,url in IMAGES.items():
  target=DEST/name
  if target.exists():
   data=target.read_bytes()
  else:
   req=Request(url,headers={"User-Agent":"ATWash-AssetImporter/1.0"})
   with urlopen(req,timeout=25) as response:
    if not response.headers.get("Content-Type","").lower().startswith("image/"):
     raise ValueError(f"Not an image: {name}")
    data=response.read(6_000_001)
   if len(data)>6_000_000 or len(data)<2000:
    raise ValueError(f"Unexpected image size: {name}")
   if not (data.startswith(b"\xff\xd8\xff") or data.startswith(b"\x89PNG") or data.startswith(b"RIFF") or data.startswith(b"\x00\x00\x00")):
    raise ValueError(f"Unexpected image format: {name}")
   target.write_bytes(data)
  manifest[name]={"sha256":hashlib.sha256(data).hexdigest(),"source":url}
  print(f"{name}: {len(data)} bytes")
 (DEST/"manifest.json").write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
if __name__=="__main__":main()
