from playwright.sync_api import sync_playwright
from pathlib import Path
import json, traceback
P=Path(__file__).resolve().parent.parent/'demo-preview'
html=(P/'index.html').read_text(encoding='utf-8')
css=(P/'styles.css').read_text(encoding='utf-8')
js=(P/'preview.js').read_text(encoding='utf-8')
html=html.replace('<link href="styles.css" rel="stylesheet">',f'<style>{css}</style>').replace('<script src="preview.js"></script>',f'<script>{js}</script>')
tests=[]
def verify(name, ok, details=''):
  tests.append({'test':name,'status':'PASS' if ok else 'FAIL','details':details})
  print(tests[-1])
with sync_playwright() as p:
  import os
  exe=Path('/usr/bin/chromium')
  options={'headless':True}
  if os.name != 'nt' and exe.is_file(): options['executable_path']=str(exe); options['args']=['--no-sandbox']
  browser=p.chromium.launch(**options)
  page=browser.new_page(viewport={'width':1440,'height':900})
  errors=[]; dialogs=[]
  page.on('pageerror',lambda e:errors.append(str(e)))
  def dialog_handler(d):
    dialogs.append({'type':d.type,'message':d.message}); d.accept()
  page.on('dialog',dialog_handler)
  try:
    page.evaluate("""() => { const mem = {}; Object.defineProperty(window, 'localStorage', {configurable:true, value:{getItem:k=>mem[k]||null, setItem:(k,v)=>mem[k]=String(v),removeItem:k=>delete mem[k], clear:()=>Object.keys(mem).forEach(k=>delete mem[k])}}) }""")
    page.set_content(html,wait_until='domcontentloaded',timeout=15000)
    verify('PV-01 Giao diện tải và render',page.locator('h1').count()==1 and page.locator('.nav-item').count()==7, 'header='+','.join(page.locator('h1').all_text_contents()))
    verify('PV-02 Dữ liệu ban đầu',page.evaluate('state().bookings.length')==4 and page.evaluate('state().rooms.length')==18)
    page.locator('.nav-item[data-page="bookings"]').click()
    page.get_by_role('button',name='＋ Tạo đặt phòng').click()
    verify('PV-03 Hiện biểu mẫu đặt phòng',page.locator('#guest').count()==1 and page.locator('#bookRoom').count()==1)
    page.locator('#guest').fill('E2E UI Test Khách')
    page.locator('#phone').fill('0912342222')
    page.locator('#bookRoom').select_option('5')
    page.get_by_role('button',name='Xác nhận đặt phòng').click()
    b=page.evaluate("state().bookings.find(x=>x.guest==='E2E UI Test Khách')")
    verify('PV-04 Tạo đặt phòng',b is not None and b['room']==5 and b['status']=='CONFIRMED',str(b))
    page.evaluate('(id)=>checkin(id)',b['id'])
    s=page.evaluate('state().bookings.find(x=>x.guest===\'E2E UI Test Khách\')')
    verify('PV-05 Check-in đổi trạng thái',s['status']=='CHECKED_IN')
    page.evaluate('(id)=>folioDialog(id)',b['id'])
    verify('PV-06 Mở folio',page.locator('#modalTitle').inner_text().startswith('Folio'))
    stock_before=page.evaluate('state().services.find(s=>s.id===1).stock')
    page.evaluate('(id)=>serviceDialog(id)',b['id'])
    page.locator('#svc').select_option('1');page.locator('#qty').fill('2')
    page.get_by_role('button',name='Ghi nhận').click()
    after=page.evaluate('state().bookings.find(x=>x.guest===\'E2E UI Test Khách\')')
    stock_after=page.evaluate('state().services.find(s=>s.id===1).stock')
    verify('PV-07 Ghi dịch vụ + trừ kho',stock_before-stock_after==2 and len(after['services'])==1)
    balance=page.evaluate('balance(state().bookings.find(x=>x.guest===\'E2E UI Test Khách\'))')
    page.evaluate('(id)=>payDialog(id)',b['id'])
    page.locator('#amount').fill(str(balance));page.get_by_role('button',name='Xác nhận thu').click()
    remaining=page.evaluate('balance(state().bookings.find(x=>x.guest===\'E2E UI Test Khách\'))')
    verify('PV-08 Thanh toán đủ',remaining==0, f'charged={balance},balance={remaining}')
    page.evaluate('(id)=>checkout(id)',b['id'])
    after=page.evaluate('state().bookings.find(x=>x.guest===\'E2E UI Test Khách\')')
    room=page.evaluate('state().rooms.find(x=>x.id===5)')
    verify('PV-09 Checkout → DIRTY',after['status']=='CHECKED_OUT' and room['clean']=='DIRTY')
    page.evaluate('clean(5)');verify('PV-10 Dọn phòng → CLEAN',page.evaluate('state().rooms.find(x=>x.id===5).clean')=='CLEAN')
    page.evaluate('clean(5)');verify('PV-11 Duyệt phòng → INSPECTED',page.evaluate('state().rooms.find(x=>x.id===5).clean')=='INSPECTED')
    page.evaluate("document.querySelector('#propertySelect').value='DN'; document.querySelector('#propertySelect').dispatchEvent(new Event('change',{bubbles:true}))")
    verify('PV-12 Chuyển cơ sở giữ dữ liệu tách biệt',page.evaluate('state().rooms.length')==10 and page.evaluate('state().bookings.length')==0)

    # Negative cases in Hà Nội demo data. These are browser-side checks only.
    page.evaluate("document.querySelector('#propertySelect').value='HN'; document.querySelector('#propertySelect').dispatchEvent(new Event('change',{bubbles:true}))")
    initial_count = page.evaluate('state().bookings.length')
    page.evaluate('bookingDialog(3)')
    page.locator('#guest').fill('Booking cố tình trùng phòng')
    page.locator('#bookRoom').select_option('3')
    page.get_by_role('button',name='Xác nhận đặt phòng').click()
    verify('PV-13 Chặn đặt trùng phòng',page.evaluate('state().bookings.length')==initial_count and any('trùng lịch' in d['message'] for d in dialogs))
    page.evaluate('closeModal()')
    page.evaluate('folioDialog(2)')
    old_status = page.evaluate('state().bookings.find(x=>x.id===2).status')
    page.evaluate('checkout(2)')
    verify('PV-14 Chặn checkout chưa thanh toán đủ',page.evaluate('state().bookings.find(x=>x.id===2).status')==old_status and any('thanh toán đủ' in d['message'] for d in dialogs))
    before = page.evaluate('state().services.find(x=>x.id===1).stock')
    page.evaluate('serviceDialog(2)')
    page.locator('#svc').select_option('1')
    page.locator('#qty').fill('99999')
    page.get_by_role('button',name='Ghi nhận').click()
    verify('PV-15 Chặn bán quá tồn kho',page.evaluate('state().services.find(x=>x.id===1).stock')==before and any('Không đủ tồn' in d['message'] for d in dialogs))
    verify('PV-16 Không có lỗi JavaScript',len(errors)==0,str(errors))
    
    page.screenshot(path=str(Path(__file__).resolve().parent/'preview_test_screenshot.png'),full_page=True)
  except Exception as exc:
    print('EXCEPTION',str(exc));print(traceback.format_exc()); verify('PV-UNEXPECTED',False,str(exc))
  finally:
    browser.close()
((Path(__file__).resolve().parent/'preview_results.json')).write_text(json.dumps({'tests':tests,'jsErrors':errors,'dialogs':dialogs},ensure_ascii=False,indent=2),encoding='utf-8')
