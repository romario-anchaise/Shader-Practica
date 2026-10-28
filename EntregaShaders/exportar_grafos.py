"""Export logical diagrams from the saved Unity Shader Graphs, without changing them.

These are explicitly identified as exports, not screenshots of Unity's editor.
Run with Python + Pillow from this project directory.
"""
from pathlib import Path
import hashlib
import html
import json
import math
from PIL import Image, ImageDraw, ImageFont

PROJECT = Path(__file__).resolve().parent.parent
OUTPUT = PROJECT / "EntregaShaders/Evidencias/Grafos"
OUTPUT.mkdir(parents=True, exist_ok=True)
FONT = Path("C:/Windows/Fonts/segoeui.ttf")


def read_graph(path):
    text = path.read_text(encoding="utf-8-sig")
    decoder, objects, i = json.JSONDecoder(), [], 0
    while i < len(text):
        while i < len(text) and text[i].isspace():
            i += 1
        if i == len(text):
            break
        obj, i = decoder.raw_decode(text, i)
        objects.append(obj)
    return objects[0], {o["m_ObjectId"]: o for o in objects}


def export(path):
    root, objects = read_graph(path)
    nodes = {r["m_Id"]: objects[r["m_Id"]] for r in root["m_Nodes"]}
    blocks = {k: n for k, n in nodes.items() if n["m_Type"].endswith("BlockNode")}
    nodes = {k: n for k, n in nodes.items() if k not in blocks}
    master_slots = []
    for ref in root["m_FragmentContext"]["m_Blocks"]:
        block = objects[ref["m_Id"]]
        slot = objects[block["m_Slots"][0]["m_Id"]]
        master_slots.append({"m_Id": ref["m_Id"], "m_DisplayName": block["m_Name"].replace("SurfaceDescription.", ""), "m_SlotType": 0, "m_Value": slot.get("m_Value")})
    nodes["fragment"] = {"m_Name": "Fragment / Master Stack", "m_Type": "Master", "_slots": master_slots, "m_DrawState": {"m_Position": {"y": 300}}}

    def slots(node):
        return node.get("_slots", [objects[r["m_Id"]] for r in node.get("m_Slots", [])])

    edges = []
    for edge in root["m_Edges"]:
        a, b = edge["m_OutputSlot"], edge["m_InputSlot"]
        source, target = a["m_Node"]["m_Id"], b["m_Node"]["m_Id"]
        slot = b["m_SlotId"]
        if target in blocks:
            slot, target = target, "fragment"
        if source in nodes and target in nodes:
            edges.append((source, a["m_SlotId"], target, slot))
    depth = {k: 0 for k in nodes}
    for _ in range(len(nodes)):
        changed = False
        for a, _, b, _ in edges:
            if depth[b] <= depth[a]:
                depth[b] = depth[a] + 1
                changed = True
        if not changed:
            break
    depth["fragment"] = max(depth.values())
    columns = {}
    for key in nodes:
        columns.setdefault(depth[key], []).append(key)
    layout = {}
    for column, keys in columns.items():
        y = 170
        for key in sorted(keys, key=lambda k: nodes[k].get("m_DrawState", {}).get("m_Position", {}).get("y", 0)):
            height = 66 + max(1, len(slots(nodes[key]))) * 27
            layout[key] = (50 + column * 330, y, 265, height)
            y += height + 45
    width = max(x + w for x, y, w, h in layout.values()) + 50
    height = max(y + h for x, y, w, h in layout.values()) + 95
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
           '<rect width="100%" height="100%" fill="#101924"/>',
           '<style>text{font-family:Segoe UI,Arial,sans-serif;fill:#e3edf4;font-size:16px}.small{font-size:13px;fill:#9fb7c7}</style>',
           f'<text x="50" y="48" style="font-size:30px;font-weight:700">{path.stem}</text>',
           '<text x="50" y="78" class="small">DIAGRAMA EXPORTADO DE LAS CONEXIONES REALES / No es una captura de la interfaz de Unity</text>',
           f'<text x="50" y="103" class="small">{len(nodes)-1} nodos + Fragment | {len(edges)} conexiones | SHA256: {hashlib.sha256(path.read_bytes()).hexdigest()[:20]}</text>']
    image = Image.new("RGB", (width, height), "#101924")
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype(str(FONT), 16)
    small = ImageFont.truetype(str(FONT), 13)
    title_font = ImageFont.truetype(str(FONT), 30)
    draw.text((50, 18), path.stem, font=title_font, fill="#e3edf4")
    draw.text((50, 64), "DIAGRAMA EXPORTADO DE LAS CONEXIONES REALES / No es una captura de Unity", font=small, fill="#9fb7c7")
    draw.text((50, 89), f"{len(nodes)-1} nodos + Fragment | {len(edges)} conexiones", font=small, fill="#9fb7c7")

    def endpoint(key, slot_id, output):
        x, y, w, h = layout[key]
        row = next((i for i, s in enumerate(slots(nodes[key])) if s["m_Id"] == slot_id), 0)
        return (x + w if output else x, y + 70 + row * 27)

    for index, (a, slot_a, b, slot_b) in enumerate(edges):
        x1, y1 = endpoint(a, slot_a, True)
        x2, y2 = endpoint(b, slot_b, False)
        color = ["#4bd5c4", "#6ab4f5", "#c5a0ed", "#efbb6c"][index % 4]
        bend = max(45, abs(x2-x1) * 0.42)
        svg.append(f'<path d="M{x1},{y1} C{x1+bend},{y1} {x2-bend},{y2} {x2},{y2}" fill="none" stroke="{color}" stroke-width="2.5" opacity="0.8"/>')
        points = []
        for i in range(41):
            t = i / 40
            u = 1-t
            points.append((u**3*x1 + 3*u*u*t*(x1+bend) + 3*u*t*t*(x2-bend) + t**3*x2,
                           u**3*y1 + 3*u*u*t*y1 + 3*u*t*t*y2 + t**3*y2))
        draw.line(points, fill=color, width=3)
    for key, node in nodes.items():
        x, y, w, h = layout[key]
        is_property = node["m_Type"].endswith("PropertyNode")
        name = node.get("m_Name", "Node")
        if is_property:
            prop = objects.get(node.get("m_Property", {}).get("m_Id"), {})
            name = prop.get("m_Name", name)
        header = "#174c50" if is_property else "#263f62" if key == "fragment" else "#2d3b4d"
        svg.extend([f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="8" fill="#1c2938" stroke="#486174"/>',
                    f'<rect x="{x}" y="{y}" width="{w}" height="40" rx="8" fill="{header}"/>',
                    f'<text x="{x+14}" y="{y+27}" style="font-weight:600">{html.escape(name[:29])}</text>'])
        draw.rounded_rectangle((x,y,x+w,y+h), radius=8, fill="#1c2938", outline="#486174", width=2)
        draw.rounded_rectangle((x,y,x+w,y+40), radius=8, fill=header)
        draw.text((x+14,y+10), name[:29], font=font, fill="#e3edf4")
        for i, slot in enumerate(slots(node)):
            output = slot.get("m_SlotType") == 1
            sx, sy = x+w if output else x, y+70+i*27
            label = slot.get("m_DisplayName", "Value")
            val = slot.get("m_Value")
            if isinstance(val, (float, int)) and not output:
                label += " = " + f"{val:.3g}"
            svg.append(f'<circle cx="{sx}" cy="{sy}" r="5" fill="#66dfcc"/>')
            svg.append(f'<text x="{x+14}" y="{sy+5}" class="small">{html.escape(label[:34])}{"  >" if output else ""}</text>')
            draw.ellipse((sx-5,sy-5,sx+5,sy+5),fill="#66dfcc")
            draw.text((x+14,sy-8), label[:34] + ("  >" if output else ""), font=small, fill="#b8cdda")
    svg.append(f'<text x="50" y="{height-28}" class="small">Fuente: {html.escape(str(path.relative_to(PROJECT)).replace(chr(92), "/"))}</text></svg>')
    (OUTPUT / f"{path.stem}.svg").write_text("\n".join(svg), encoding="utf-8")
    # Retain full readable resolution; SVG is available for lossless zoom.
    image.save(OUTPUT / f"{path.stem}.png", optimize=True)
    return {"name": path.stem, "properties": len(root["m_Properties"]), "nodes": len(nodes)-1, "edges": len(edges)}


manifest = [export(p) for p in sorted((PROJECT / "Assets/_MyShaders").rglob("*.shadergraph"))]
(OUTPUT / "inventario.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
cards = "\n".join(f'<article><h2>{item["name"]}</h2><p>{item["properties"]} propiedades, {item["nodes"]} nodos y {item["edges"]} conexiones. <a href="{item["name"]}.svg">Abrir SVG y ampliar</a></p><a href="{item["name"]}.svg"><img src="{item["name"]}.png" alt="Grafo {item["name"]}"></a></article>' for item in manifest)
(OUTPUT / "index.html").write_text('<!doctype html><html lang="es"><meta charset="utf-8"><title>Shander — Grafos</title><style>body{background:#101924;color:#e3edf4;font:17px system-ui;margin:40px;max-width:1600px}a{color:#66dfcc}article{margin:40px 0;border-top:1px solid #486174}img{width:100%;max-height:800px;object-fit:contain;background:#101924}</style><h1>Shander / Shader Graphs</h1><p>Diagramas exportados desde los archivos reales del proyecto. No son capturas de la interfaz de Unity. Abre el SVG para leer todos los nodos con zoom.</p>'+cards+'</html>',encoding="utf-8")
print(json.dumps(manifest, ensure_ascii=False, indent=2))

frames = sorted((PROJECT / "EntregaShaders/Evidencias/KataFrames").glob("frame_*.png"))
if frames:
    images = [Image.open(p).convert("RGB") for p in frames]
    images[0].save(PROJECT / "EntregaShaders/Evidencias/MaterialImposible_CristalDeMarea.gif", save_all=True, append_images=images[1:], duration=120, loop=0, optimize=False)
    print(f"GIF: {len(images)} frames, {len({hashlib.sha256(p.read_bytes()).hexdigest() for p in frames})} distinct rendered frames")
