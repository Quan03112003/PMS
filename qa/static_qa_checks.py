"""Lightweight static cross-checks only; not a C# or Angular compiler."""
from pathlib import Path
import re, json, sys
P=Path(__file__).resolve().parent.parent
back=P/'backend/HotelPms.Api'
front=P/'frontend/src/app'
api=[]
for f in back.glob('*.cs'):
    for method,route in re.findall(r'\.Map(Get|Post|Patch|Put|Delete)\("([^"]+)"',f.read_text(encoding='utf-8')):
        api.append((method.upper(),route))
def normalize(s):
    s=s.split('?')[0].removeprefix('/api')
    s=re.sub(r'\$\{[^}]+\}',':param',s)
    s=re.sub(r'\{[^}]+\}',':param',s)
    return s
ui=[]
for f in front.glob('*.ts'):
    for method,quote,route in re.findall(r'this\.api\.(get|getText|post|patch)(?:<[^>]+>)?\(\s*([`\'"])(.*?)\2',f.read_text(encoding='utf-8')):
        if route=='/availability?':continue
        method='GET' if method=='getText' else method.upper()
        ui.append((method,route,f.name))
missing=[]
for method,route,file in ui:
    if (method,normalize(route)) not in {(m,normalize(r)) for m,r in api}:
        missing.append(f'{file}: {method} {route}')
# Detect duplicate inferred names in simple anonymous objects: ex. new { a.Id, b.Id }.
dup=[]
for f in back.glob('*.cs'):
    source=f.read_text(encoding='utf-8')
    for match in re.finditer(r'new\s*\{([^{}]*)\}',source,re.S):
        names=[]
        for item in match.group(1).split(','):
            item=item.strip()
            if re.match(r'^\w+\s*=',item):names.append(item.split('=',1)[0].strip())
            elif re.fullmatch(r'\w+\.\w+',item):names.append(item.split('.')[-1])
            elif re.fullmatch(r'\w+',item):names.append(item)
        repeated=set(x for x in names if names.count(x)>1)
        if repeated: dup.append(f'{f.name}:{source[:match.start()].count(chr(10))+1} keys={repeated}')
configs=['frontend/package.json','frontend/angular.json','frontend/tsconfig.json','frontend/tsconfig.app.json',
         'backend/HotelPms.Api/appsettings.json','backend/HotelPms.Api/appsettings.Development.json']
config_issues=[]
for file in configs:
    try: json.loads((P/file).read_text(encoding='utf-8'))
    except Exception as exc: config_issues.append(file+': '+str(exc))
report={'backendRoutes':len(api),'frontendDirectCallsMatched':len(ui)-len(missing),
  'missingDirectRouteMappings':missing,'duplicateAnonymousKeys':dup,'invalidJsonConfigs':config_issues}
print(json.dumps(report,ensure_ascii=False,indent=2))
sys.exit(bool(missing or dup or config_issues))
