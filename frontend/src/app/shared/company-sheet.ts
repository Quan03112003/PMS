export function openCompanySheet(id: string): void {
  const node = document.getElementById(id);
  if (!node) return;
  const page = window.open('', '_blank', 'noopener,width=980,height=760');
  if (!page) return;
  page.document.write(`<!doctype html><html><head><meta charset="utf-8"><title>Báo cáo</title><style>
    body{margin:0;padding:18px;color:#1c1c1c;background:#fff}
    .co-sheet{font-family:"Times New Roman",Times,serif}
    .co-head{display:flex;gap:18px;align-items:flex-start;border-bottom:2px solid #c4161c;padding-bottom:10px}
    .co-logo{width:92px;text-align:center;color:#c4161c}
    .co-logo b{display:block;font-family:Arial,sans-serif;font-size:11px}
    .co-id{font-family:Arial,sans-serif;font-size:12px;line-height:1.45}
    .co-id b{display:block;font-size:15px}
    .co-id span{display:block}
    h2{margin:14px 0 6px;text-align:center;font-size:18px}
    .co-meta{margin:0;text-align:center;font-size:13px}
    table{width:100%;border-collapse:collapse;margin-top:12px;font-size:13px}
    th,td{border:1px solid #222;padding:4px 6px;vertical-align:top}
    th,.stt{text-align:center}
    td.num{text-align:right;white-space:nowrap}
    .co-actions{display:none}
  </style></head><body>${node.outerHTML}</body></html>`);
  page.document.close();
  page.focus();
  page.print();
}
