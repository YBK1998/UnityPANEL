# Panel Wire Viewer

A small C++ (Dear ImGui + SQLite) desktop tool for building the CNC control
panels. Pick a part from the dropdown and see **every connection to and from
it** — wire number, From, To, the signal/short description, and the wire spec —
at an adjustable text size. Data lives in a local SQLite database and is fully
editable through the CRUD tabs.

## What it shows

- **Viewer tab** — choose a part (A11 board, VFD, a stepper drive, K1, a lamp,
  a button…) and get its wire list. A **Text size** slider scales the
  connection text from 0.7x to 3.0x (handy when you're reading it at the bench).
  *Side* tells you whether the part is the From end, the To end, or both.
  **Export CSV** writes the current list to `connections_<part>.csv`.
- **Per-wire "landed" checkboxes** — tick the **OK** box on a row once that wire
  is physically installed. The state is saved to the database (per wire, so it
  shows no matter which part you view it from), landed rows are dimmed/green, and
  a **Landed X / N** counter + progress bar tracks each part. **Hide landed**
  collapses the list down to what's still outstanding. The CSV export includes a
  `Landed` column.
- **Build Progress tab** — every wire grouped by area (230 V Mains & distribution,
  PE Earthing, 24 V E-stop / ON / OFF / K1, …) in build order, each group a collapsible section with a
  `[landed/total]` count (completed groups turn green and show `DONE`). Tick wires
  off here too; there's an overall progress bar, **Expand/Collapse all**, **Hide
  landed wires**, and the same text-size slider. Use this to work the panel
  section by section rather than part by part.
- **Landings tab** — reverse lookup by *endpoint*. Pick a terminal block, drive
  terminal, or connector pin from the (filterable) list on the left and see every
  wire landing on it on the right, with the **far end and direction**
  (`-> out` / `<- in`), signal, spec and area. Endpoints are derived
  automatically from the From/To text; tick **Terminal blocks only** (also lists the XL / XN distribution blocks) to narrow the
  list. Handy when you're populating a DIN rail block-by-block (and for spotting a
  block with too many or too few landings). Checkboxes and text size work here too.
- **Graph tab** — a **read-only node graph** (via imgui-node-editor) of the
  selected part's connections: the part is the centre node, each connected wire is
  a node showing the far endpoint + wire number, spec and signal. **'in' wires**
  (the part is the destination) sit on the **left** in the default colour; **'out'
  wires** (the part is the source) sit on the **right** and are **red**. Links turn
  green when landed. Drag empty space to pan, scroll to zoom, **Zoom to fit**,
  **Re-layout**. It only visualises connections — no links or nodes can be created
  or deleted.
- **Wires (CRUD) tab** — add / edit / delete any wire (number, from, to,
  signal, spec, area, notes), with a live filter.
- **Parts (CRUD) tab** — add / edit / delete parts. Each part has a `Match`
  field: a `;`-separated list of case-insensitive substrings. A wire belongs to
  a part if any token appears in the wire's From or To text (e.g. the A11 board uses
  the single token `A11`; the VFD lists its terminals, `VFD L;VFD N;…`, so it
  does not also pick up the `CB-VFD` breaker).

## Data source

On first run the database `panelwire.db` is empty, so it is seeded from
`src/seed_data.h`: the **United Box A11, wiring rev 0.7** schedule (109 wires,
101–348) and its 40 parts. Endpoint text matches
`A11_CircuitSim/Verification/A11_Wire_Schedule_rev07.md`; `(2x)` marks a twin
ferrule. After that the database is the source of truth and your CRUD edits
persist. **File → Reset DB to spec defaults** rebuilds from the seed.

`src/seed_data.h` is generated, not hand-edited. When the netlist changes, rerun:

```bat
python -I gen_seed.py ..\..\A11_CircuitSim\Verification\netlist_rev07.json src\seed_data.h
```

Signal descriptions, build areas and the parts list live in `gen_seed.py`. The
script fails if a wire has no area or signal, or if an endpoint matches no part
or more than one. Then rebuild, and reset the DB (or delete `panelwire.db`).
A reset clears the landed ticks.

The old two-box NVEM version of this tool, with its database, is archived in
`Electrical_old_NVEM/tools/PanelWireViewer`.

## Build (Windows, Visual Studio 2022 + vcpkg)

Dependencies (`imgui`, `imgui-node-editor`, `glfw3`, `sqlite3`) are declared in
`vcpkg.json` and are fetched/built automatically on the first configure (this
first run can take a while).

```bat
build.bat
```

This produces `build\Release\PanelWireViewer.exe`. If your vcpkg is not at
`C:\vcpkg`, edit `VCPKG_ROOT` at the top of `build.bat`.

Run it from this folder so `panelwire.db` and the UI layout file
`panelwire_ui.ini` are created alongside the executable.

## Files

| File | Purpose |
|------|---------|
| `src/main.cpp` | ImGui UI: viewer, CRUD tabs, font scaling, CSV export |
| `src/db.h` / `src/db.cpp` | SQLite schema, seeding, queries, CRUD |
| `src/seed_data.h` | Wires + parts, generated by `gen_seed.py` from the rev 0.7 netlist |
| `gen_seed.py` | Seed generator: signal text, build areas, parts list, match check |
| `resources/app.svg` | Source icon (a wiring node-graph) |
| `resources/app.rc`, `resources/app.ico` | Embedded application icon (rasterized from `app.svg`) |
| `CMakeLists.txt`, `vcpkg.json`, `build.bat` | Build setup |

### Changing the icon

The icon is designed in `resources/app.svg` and rasterized to a multi-size
`resources/app.ico` (256/48/32/16) with ImageMagick:

```bat
cd resources
magick -background none app.svg -resize 256x256 -define icon:auto-resize=256,48,32,16 app.ico
```

Edit `app.svg`, re-run that command, and rebuild. The icon is embedded via
`resources/app.rc` (`1 ICON "app.ico"`).
