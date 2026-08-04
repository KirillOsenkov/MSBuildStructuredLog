using System.Text.Json;
using System.Text.Json.Serialization;

namespace BinlogTool
{
    internal static class StatsHtmlWriter
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            // note: the default encoder escapes '<' and '>' so the JSON is safe inside a <script> tag
        };

        public static string Write(StatsReport report)
        {
            string json = JsonSerializer.Serialize(report, jsonOptions);
            return Template
                .Replace("__TITLE__", System.Net.WebUtility.HtmlEncode(report.FileName))
                .Replace("__DATA__", json);
        }

        private const string Template = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Binlog stats — __TITLE__</title>
<style>
  :root {
    color-scheme: light dark;
    --page: #f9f9f7;
    --surface: #fcfcfb;
    --ink: #0b0b0b;
    --ink2: #52514e;
    --muted: #898781;
    --grid: #e1e0d9;
    --baseline: #c3c2b7;
    --border: rgba(11,11,11,.10);
    --s1: #2a78d6;
    --s2: #eb6834;
    --s3: #1baf7a;
    --s4: #eda100;
    --s5: #e87ba4;
    --other: #c3c2b7;
    --bar: #2a78d6;
  }
  @media (prefers-color-scheme: dark) {
    :root {
      --page: #0d0d0d;
      --surface: #1a1a19;
      --ink: #ffffff;
      --ink2: #c3c2b7;
      --muted: #898781;
      --grid: #2c2c2a;
      --baseline: #383835;
      --border: rgba(255,255,255,.10);
      --s1: #3987e5;
      --s2: #d95926;
      --s3: #199e70;
      --s4: #c98500;
      --s5: #d55181;
      --other: #52514e;
      --bar: #3987e5;
    }
  }
  * { box-sizing: border-box; }
  body {
    margin: 0;
    background: var(--page);
    color: var(--ink);
    font-family: system-ui, -apple-system, "Segoe UI", sans-serif;
    font-size: 14px;
    line-height: 1.45;
  }
  .page { max-width: 1150px; margin: 0 auto; padding: 24px 20px 40px; }
  header h1 { font-size: 18px; font-weight: 600; margin: 0 0 2px; }
  header .file { font-size: 24px; font-weight: 650; overflow-wrap: anywhere; }
  header .meta { color: var(--muted); font-size: 12px; margin-top: 4px; overflow-wrap: anywhere; }
  .mono { font-family: ui-monospace, Consolas, monospace; }

  .kpis { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: 10px; margin: 18px 0; }
  .tile { background: var(--surface); border: 1px solid var(--border); border-radius: 10px; padding: 12px 14px; }
  .tile .v { font-size: 22px; font-weight: 600; }
  .tile .l { color: var(--muted); font-size: 11px; text-transform: uppercase; letter-spacing: .04em; margin-bottom: 4px; }
  .tile .s { color: var(--ink2); font-size: 12px; margin-top: 2px; font-variant-numeric: tabular-nums; }

  .card { background: var(--surface); border: 1px solid var(--border); border-radius: 10px; padding: 16px 18px; margin: 14px 0; }
  .card h2 { font-size: 15px; font-weight: 600; margin: 0 0 4px; }
  .note { color: var(--ink2); font-size: 12.5px; margin: 0 0 12px; }

  .stack { display: flex; column-gap: 2px; height: 36px; margin: 10px 0 12px; }
  .seg { min-width: 3px; display: flex; align-items: center; justify-content: center; overflow: hidden; }
  .seg:first-child { border-radius: 4px 0 0 4px; }
  .seg:last-child { border-radius: 0 4px 4px 0; }
  .seg .seglabel { color: #fff; font-size: 11.5px; font-weight: 600; white-space: nowrap; text-shadow: 0 1px 2px rgba(0,0,0,.35); }
  .legend { display: flex; flex-wrap: wrap; gap: 6px 18px; }
  .legend .li { display: flex; align-items: center; gap: 7px; font-size: 12.5px; color: var(--ink2); }
  .legend .chipc { width: 10px; height: 10px; border-radius: 3px; flex: none; }
  .legend .nm { color: var(--ink); font-weight: 550; }
  .legend .nums { font-variant-numeric: tabular-nums; }

  .bars { display: grid; grid-template-columns: minmax(180px, 34%) 1fr 110px; gap: 6px 10px; align-items: center; }
  .bars .blabel { font-size: 12.5px; color: var(--ink); text-align: right; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; direction: rtl; }
  .bars .btrack { height: 20px; display: flex; align-items: center; }
  .bars .bfill { height: 20px; background: var(--bar); border-radius: 0 4px 4px 0; min-width: 2px; }
  .bars .bval { font-size: 12.5px; color: var(--ink2); font-variant-numeric: tabular-nums; white-space: nowrap; }

  .toolbar { margin: 0 0 10px; display: flex; gap: 8px; }
  .toolbar button {
    background: var(--surface); color: var(--ink2); border: 1px solid var(--baseline);
    border-radius: 6px; padding: 4px 10px; font-size: 12.5px; cursor: pointer;
  }
  .toolbar button:hover { color: var(--ink); border-color: var(--muted); }

  .tgrid { display: grid; grid-template-columns: minmax(240px, 1fr) 110px 64px 105px 95px 150px; gap: 8px; align-items: center; }
  .thead { color: var(--muted); font-size: 11px; text-transform: uppercase; letter-spacing: .04em; padding: 4px 6px; border-bottom: 1px solid var(--grid); }
  .trow { padding: 3px 6px; border-radius: 6px; }
  .trow:hover { background: color-mix(in srgb, var(--bar) 7%, transparent); }
  .trow.expandable { cursor: pointer; }
  .tname { display: flex; align-items: center; gap: 6px; min-width: 0; }
  .tname .nm { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .caret { width: 14px; flex: none; color: var(--muted); font-size: 11px; }
  .num { text-align: right; font-variant-numeric: tabular-nums; color: var(--ink2); font-size: 12.5px; white-space: nowrap; }
  .num.size { color: var(--ink); }
  .cellbar { height: 8px; background: color-mix(in srgb, var(--grid) 55%, transparent); border-radius: 3px; overflow: hidden; }
  .cellbar i { display: block; height: 8px; background: var(--bar); border-radius: 0 3px 3px 0; }

  .kids { border-left: 1px solid var(--grid); margin-left: 12px; }

  .sgroup { margin: 4px 0 10px 34px; }
  .shead { margin-bottom: 3px; }
  .chip {
    display: inline-block; font-size: 10.5px; font-weight: 600; letter-spacing: .03em;
    color: var(--ink2); background: color-mix(in srgb, var(--grid) 60%, transparent);
    border-radius: 4px; padding: 1px 7px; text-transform: uppercase;
  }
  .sample { display: flex; gap: 10px; padding: 2px 0; align-items: baseline; }
  .sample .slen { color: var(--muted); font-size: 11px; white-space: nowrap; font-variant-numeric: tabular-nums; flex: none; min-width: 80px; text-align: right; }
  .sample code { font-family: ui-monospace, Consolas, monospace; font-size: 12px; color: var(--ink2); white-space: pre-wrap; overflow-wrap: anywhere; }

  .strrow { display: flex; gap: 10px; padding: 4px 0; border-bottom: 1px solid var(--grid); align-items: baseline; }
  .strrow .slen { color: var(--muted); font-size: 11px; white-space: nowrap; font-variant-numeric: tabular-nums; flex: none; min-width: 90px; text-align: right; }
  .strrow code { font-family: ui-monospace, Consolas, monospace; font-size: 12px; color: var(--ink2); white-space: pre-wrap; overflow-wrap: anywhere; max-height: 4.5em; overflow: hidden; display: block; cursor: pointer; }
  .strrow code.open { max-height: none; }

  footer { color: var(--muted); font-size: 12px; margin-top: 22px; }
  footer a { color: var(--ink2); }

  #tooltip {
    position: fixed; z-index: 10; pointer-events: none; max-width: 480px;
    background: var(--surface); color: var(--ink); border: 1px solid var(--baseline);
    border-radius: 8px; padding: 8px 10px; font-size: 12.5px;
    box-shadow: 0 4px 16px rgba(0,0,0,.18);
  }
  #tooltip .t { font-weight: 600; margin-bottom: 2px; overflow-wrap: anywhere; }
  #tooltip .d { color: var(--ink2); font-variant-numeric: tabular-nums; }

  @media (max-width: 760px) {
    .tgrid { grid-template-columns: minmax(160px, 1fr) 100px 56px; }
    .tgrid .hide-sm { display: none; }
    .bars { grid-template-columns: minmax(120px, 40%) 1fr 90px; }
  }
</style>
</head>
<body>
<div class="page">
  <header>
    <h1>Binlog statistics</h1>
    <div class="file" id="fileName"></div>
    <div class="meta" id="fileMeta"></div>
  </header>

  <section class="kpis" id="kpis"></section>

  <section class="card">
    <h2>What takes up the uncompressed stream</h2>
    <p class="note">Strings are deduplicated and stored once in the string table; event records only reference
    them, so a record bucket can be small even when its message texts are long. Consider the record buckets
    and the string table together.</p>
    <div class="stack" id="stack"></div>
    <div class="legend" id="stackLegend"></div>
  </section>

  <section class="card">
    <h2>Largest buckets</h2>
    <p class="note">Deepest categories by total bytes across the whole hierarchy — the WinDirStat view of the binlog.</p>
    <div class="bars" id="bars"></div>
  </section>

  <section class="card">
    <h2>Drill down</h2>
    <p class="note">Click a row to expand subcategories and sample record texts. “Largest records” samples are the
    biggest records in a bucket; pNN samples sit at the NNth size percentile. <span class="mono">len</span> is the full
    text length in characters; samples are deduplicated and truncated.</p>
    <div class="toolbar">
      <button id="expandBtn" type="button">Expand two levels</button>
      <button id="collapseBtn" type="button">Collapse all</button>
    </div>
    <div class="thead tgrid">
      <div>Bucket</div><div style="text-align:right">Total size</div><div style="text-align:right">%</div>
      <div style="text-align:right" class="hide-sm">Count</div><div style="text-align:right" class="hide-sm">Largest</div><div class="hide-sm"></div>
    </div>
    <div id="tree"></div>
  </section>

  <section class="card" id="stringsCard" hidden>
    <h2>Largest strings</h2>
    <p class="note" id="stringsNote"></p>
    <div id="strings"></div>
  </section>

  <footer>Generated by <span class="mono">binlogtool stats</span> · <span id="genTime"></span> ·
    <a href="https://msbuildlog.com">msbuildlog.com</a></footer>
</div>
<div id="tooltip" hidden></div>
<script id="data" type="application/json">__DATA__</script>
<script>
(function () {
  const data = JSON.parse(document.getElementById('data').textContent);
  const fmt = n => (n ?? 0).toLocaleString('en-US');
  function human(n) {
    n = n || 0;
    if (n < 1024) return fmt(n) + ' B';
    const units = ['KB', 'MB', 'GB', 'TB'];
    let v = n, i = -1;
    do { v /= 1024; i++; } while (v >= 1024 && i < units.length - 1);
    return (v >= 100 ? v.toFixed(0) : v.toFixed(1)) + ' ' + units[i];
  }
  const rootSize = data.root ? data.root.size : 0;
  const total = Math.max(1,
    rootSize + (data.strings?.size || 0) + (data.nameValueLists?.size || 0) + (data.blobs?.size || 0));
  const pctText = n => { const p = n * 100 / total; return p >= 10 ? p.toFixed(1) + '%' : p.toFixed(2) + '%'; };
  function el(tag, cls, text) {
    const e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }

  // ---- tooltip -------------------------------------------------------------
  const tip = document.getElementById('tooltip');
  function bindTip(target, makeLines) {
    target.addEventListener('mousemove', e => {
      tip.hidden = false;
      tip.textContent = '';
      const lines = makeLines();
      tip.appendChild(el('div', 't', lines[0]));
      for (let i = 1; i < lines.length; i++) tip.appendChild(el('div', 'd', lines[i]));
      const pad = 14;
      let x = e.clientX + pad, y = e.clientY + pad;
      const r = tip.getBoundingClientRect();
      if (x + r.width + 8 > innerWidth) x = e.clientX - r.width - pad;
      if (y + r.height + 8 > innerHeight) y = e.clientY - r.height - pad;
      tip.style.left = Math.max(4, x) + 'px';
      tip.style.top = Math.max(4, y) + 'px';
    });
    target.addEventListener('mouseleave', () => { tip.hidden = true; });
  }

  // ---- header --------------------------------------------------------------
  document.getElementById('fileName').textContent = data.fileName;
  document.getElementById('fileMeta').textContent =
    data.filePath + ' · file format version ' + data.fileFormatVersion;
  document.getElementById('genTime').textContent = data.generated;

  // ---- KPI tiles -----------------------------------------------------------
  const kpis = [
    { label: 'File size on disk', value: human(data.fileSize), sub: fmt(data.fileSize) + ' bytes' },
    { label: 'Uncompressed', value: human(total), sub: (total / Math.max(1, data.fileSize)).toFixed(1) + '× compression' },
    { label: 'Event records', value: fmt(data.recordCount), sub: human(rootSize) + ' of record data' },
    { label: 'Unique strings', value: fmt(data.strings?.count), sub: human(data.strings?.size) + ' string table' },
    { label: 'Largest string', value: human(data.strings?.largest), sub: fmt(data.strings?.largest) + ' bytes' }
  ];
  const kpisEl = document.getElementById('kpis');
  for (const k of kpis) {
    const t = el('div', 'tile');
    t.appendChild(el('div', 'l', k.label));
    t.appendChild(el('div', 'v', k.value));
    t.appendChild(el('div', 's', k.sub));
    kpisEl.appendChild(t);
  }

  // ---- composition (stacked bar, part-to-whole) ----------------------------
  const comp = [];
  for (const c of (data.root?.children || [])) comp.push({ name: c.name, size: c.size });
  if (!((data.root?.children) || []).length && data.root) comp.push({ name: data.root.name, size: data.root.size });
  if (data.strings?.size) comp.push({ name: 'Strings', size: data.strings.size });
  if (data.nameValueLists?.size) comp.push({ name: 'NameValueLists', size: data.nameValueLists.size });
  if (data.blobs?.size) comp.push({ name: 'Blobs', size: data.blobs.size });
  comp.sort((a, b) => b.size - a.size);
  const segments = comp.slice(0, 5).filter(c => c.size > 0);
  const restSum = comp.slice(5).reduce((s, c) => s + c.size, 0);
  if (restSum > 0) segments.push({ name: 'Other', size: restSum, other: true });
  const colors = ['var(--s1)', 'var(--s2)', 'var(--s3)', 'var(--s4)', 'var(--s5)'];
  const stackEl = document.getElementById('stack');
  const legendEl = document.getElementById('stackLegend');
  segments.forEach((seg, i) => {
    const d = el('div', 'seg');
    d.style.flexGrow = seg.size;
    d.style.flexBasis = '0';
    d.style.background = seg.other ? 'var(--other)' : colors[i];
    const p = seg.size * 100 / total;
    if (p >= 12) d.appendChild(el('span', 'seglabel', seg.name + ' ' + pctText(seg.size)));
    bindTip(d, () => [seg.name, human(seg.size) + ' · ' + fmt(seg.size) + ' bytes · ' + pctText(seg.size)]);
    stackEl.appendChild(d);

    const li = el('div', 'li');
    const chip = el('span', 'chipc');
    chip.style.background = seg.other ? 'var(--other)' : colors[i];
    li.appendChild(chip);
    li.appendChild(el('span', 'nm', seg.name));
    li.appendChild(el('span', 'nums', human(seg.size) + ' · ' + pctText(seg.size)));
    legendEl.appendChild(li);
  });

  // ---- largest buckets (bar list) ------------------------------------------
  const buckets = [];
  (function walk(node, path) {
    const kids = node.children || [];
    if (!kids.length) {
      if (path.length) buckets.push({ path: path.join(' › '), size: node.size, count: node.count, largest: node.largest });
      return;
    }
    for (const k of kids) walk(k, path.concat(k.name));
  })(data.root || {}, []);
  if (data.strings?.size) buckets.push({ path: 'Strings (string table)', size: data.strings.size, count: data.strings.count, largest: data.strings.largest });
  if (data.nameValueLists?.size) buckets.push({ path: 'NameValueLists', size: data.nameValueLists.size, count: data.nameValueLists.count, largest: data.nameValueLists.largest });
  if (data.blobs?.size) buckets.push({ path: 'Blobs (embedded files)', size: data.blobs.size, count: data.blobs.count, largest: data.blobs.largest });
  buckets.sort((a, b) => b.size - a.size);
  const top = buckets.slice(0, 15).filter(b => b.size > 0);
  const maxBucket = top.length ? top[0].size : 1;
  const barsEl = document.getElementById('bars');
  for (const b of top) {
    const label = el('div', 'blabel', b.path);
    label.title = b.path;
    const track = el('div', 'btrack');
    const fill = el('div', 'bfill');
    fill.style.width = Math.max(0.4, b.size * 100 / maxBucket) + '%';
    track.appendChild(fill);
    const val = el('div', 'bval', human(b.size) + ' · ' + pctText(b.size));
    bindTip(track, () => [
      b.path,
      human(b.size) + ' · ' + fmt(b.size) + ' bytes · ' + pctText(b.size),
      'count ' + fmt(b.count) + ' · largest ' + fmt(b.largest)
    ]);
    barsEl.appendChild(label); barsEl.appendChild(track); barsEl.appendChild(val);
  }

  // ---- drill-down tree -----------------------------------------------------
  const treeEl = document.getElementById('tree');

  function groupTitle(label) {
    return label === 'largest' ? 'largest records' : label + ' (' + label.substring(1) + 'th size percentile)';
  }

  function buildSamples(node, container) {
    for (const g of (node.sampleGroups || [])) {
      const block = el('div', 'sgroup');
      const head = el('div', 'shead');
      head.appendChild(el('span', 'chip', groupTitle(g.label)));
      block.appendChild(head);
      for (const s of g.samples) {
        const row = el('div', 'sample');
        row.appendChild(el('span', 'slen', 'len ' + fmt(s.len)));
        row.appendChild(el('code', null, s.text));
        block.appendChild(row);
      }
      container.appendChild(block);
    }
  }

  function makeRow(node, depth, container, pseudo) {
    const expandable = !pseudo &&
      ((node.children && node.children.length) || (node.sampleGroups && node.sampleGroups.length));

    const row = el('div', 'trow tgrid' + (expandable ? ' expandable' : ''));
    const nameCell = el('div', 'tname');
    nameCell.style.paddingLeft = (depth * 16) + 'px';
    const caret = el('span', 'caret', expandable ? '▸' : '');
    nameCell.appendChild(caret);
    const nm = el('span', 'nm', node.name);
    nm.title = node.name;
    nameCell.appendChild(nm);
    row.appendChild(nameCell);

    const sizeCell = el('div', 'num size', human(node.size));
    sizeCell.title = fmt(node.size) + ' bytes';
    row.appendChild(sizeCell);
    row.appendChild(el('div', 'num', pctText(node.size)));
    const countCell = el('div', 'num hide-sm', fmt(node.count));
    row.appendChild(countCell);
    row.appendChild(el('div', 'num hide-sm', fmt(node.largest)));
    const barCell = el('div', 'cellbar hide-sm');
    const barFill = el('i');
    barFill.style.width = Math.min(100, Math.max(node.size > 0 ? 0.5 : 0, node.size * 100 / total)) + '%';
    barCell.appendChild(barFill);
    row.appendChild(barCell);

    container.appendChild(row);

    const state = { node, expanded: false, kidsEl: null, childRows: [] };
    state.toggle = function (force) {
      if (!expandable) return;
      const want = force === undefined ? !state.expanded : force;
      if (want === state.expanded) return;
      if (want) {
        if (!state.kidsEl) {
          state.kidsEl = el('div', 'kids');
          for (const child of (node.children || [])) {
            state.childRows.push(makeRow(child, depth + 1, state.kidsEl, false));
          }
          buildSamples(node, state.kidsEl);
          container.insertBefore(state.kidsEl, row.nextSibling);
        }
        state.kidsEl.style.display = '';
        caret.textContent = '▾';
      } else {
        if (state.kidsEl) state.kidsEl.style.display = 'none';
        caret.textContent = '▸';
      }
      state.expanded = want;
    };

    if (expandable) {
      row.addEventListener('click', () => state.toggle());
    }

    return state;
  }

  const topNodes = (data.root?.children || []).map(n => ({ node: n, pseudo: false }));
  if (!topNodes.length && data.root) topNodes.push({ node: data.root, pseudo: false });
  if (data.strings?.size) topNodes.push({ node: { name: 'Strings (deduplicated string table)', size: data.strings.size, count: data.strings.count, largest: data.strings.largest }, pseudo: true });
  if (data.nameValueLists?.size) topNodes.push({ node: { name: 'NameValueLists (deduplicated property/metadata lists)', size: data.nameValueLists.size, count: data.nameValueLists.count, largest: data.nameValueLists.largest }, pseudo: true });
  if (data.blobs?.size) topNodes.push({ node: { name: 'Blobs (embedded files archive)', size: data.blobs.size, count: data.blobs.count, largest: data.blobs.largest }, pseudo: true });
  topNodes.sort((a, b) => b.node.size - a.node.size);
  const rootRows = topNodes.map(t => makeRow(t.node, 0, treeEl, t.pseudo));

  function expandRows(rows, depthLeft) {
    for (const r of rows) {
      r.toggle(true);
      if (depthLeft > 1) expandRows(r.childRows, depthLeft - 1);
    }
  }
  function collapseRows(rows) {
    for (const r of rows) {
      collapseRows(r.childRows);
      r.toggle(false);
    }
  }
  document.getElementById('expandBtn').addEventListener('click', () => expandRows(rootRows, 2));
  document.getElementById('collapseBtn').addEventListener('click', () => collapseRows(rootRows));

  // ---- strings -------------------------------------------------------------
  if (data.topStrings && data.topStrings.length) {
    document.getElementById('stringsCard').hidden = false;
    document.getElementById('stringsNote').textContent =
      'Top ' + data.topStrings.length + ' largest strings out of ' + fmt(data.strings?.count) +
      ' (len is characters; texts truncated — click a string to expand/collapse). ' +
      'Use "binlogtool savestrings" to dump the complete string table.';
    const stringsEl = document.getElementById('strings');
    data.topStrings.forEach((s, i) => {
      const row = el('div', 'strrow');
      row.appendChild(el('span', 'slen', (i + 1) + '. len ' + fmt(s.len)));
      const code = el('code', null, s.text);
      code.addEventListener('click', () => code.classList.toggle('open'));
      row.appendChild(code);
      stringsEl.appendChild(row);
    });
  }
})();
</script>
</body>
</html>
""";
    }
}
