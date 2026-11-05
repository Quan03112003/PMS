#!/usr/bin/env python3
"""Real Angular UI -> ASP.NET API -> SQL Server E2E (NOT offline preview).
DESTRUCTIVE: creates a demo reservation/payment and checks out. Localhost only.
Usage: py -m pip install playwright && py -m playwright install chromium
       py qa/e2e_angular_browser.py --confirm-demo
"""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import sys
import urllib.parse
import urllib.request
import uuid

from playwright.sync_api import sync_playwright


def get_json(base, path, token):
    req=urllib.request.Request(base.rstrip('/')+'/api'+path,headers={'Authorization':'Bearer '+token})
    with urllib.request.urlopen(req,timeout=20) as r:
        return json.load(r)


def run(frontend, backend):
    name='UI-E2E-'+uuid.uuid4().hex[:9].upper()
    result={'case':'UI-REAL-01','tag':name,'frontend':frontend,'backend':backend,'status':'BLOCKED','steps':[],
            'timestampUtc':datetime.now(timezone.utc).isoformat(),'errors':[]}
    report=Path(__file__).parent/'angular_e2e_results.json'
    errors=[]
    try:
        with sync_playwright() as p:
            options={'headless':True}
            if os.name!='nt' and Path('/usr/bin/chromium').is_file():
                options.update({'executable_path':'/usr/bin/chromium','args':['--no-sandbox']})
            browser=p.chromium.launch(**options)
            page=browser.new_page(viewport={'width':1440,'height':900},locale='vi-VN')
            page.set_default_timeout(20000)
            page.on('pageerror',lambda e: errors.append(str(e)))
            page.on('dialog',lambda d: d.accept())
            page.goto(frontend,wait_until='domcontentloaded',timeout=30000)
            page.locator('input[name="email"]').fill('admin@pms.local')
            page.locator('input[name="password"]').fill('Demo@12345')
            page.locator('.auth-form button[type="submit"]').click()
            page.locator('.nav-item').first.wait_for(state='visible')
            result['steps'].append('Đăng nhập Angular bằng tài khoản admin')
            token=page.evaluate('localStorage.getItem("pms_token")')
            assert token,'Không có JWT sau đăng nhập'
            props=get_json(backend,'/properties',token)
            assert props,'Không có cơ sở demo'
            property_id=props[0]['id']
            page.locator('button.nav-item').filter(has_text='Đặt phòng').first.click()
            page.get_by_role('button',name='＋ Tạo đặt phòng').click()
            room_select=page.locator('.modal .field:has(label:text("Phòng có thể bán")) select')
            room_select.locator('option').nth(1).wait_for(state='attached')
            room_select.select_option(index=1)
            page.locator('input[placeholder="Nguyễn Văn A"]').fill(name)
            page.locator('input[placeholder="09xxxxxxxx"]').fill('0900000000')
            page.get_by_role('button',name='Xác nhận đặt phòng →').click()
            booking_row=page.locator('table tbody tr').filter(has_text=name).first
            booking_row.wait_for(state='visible')
            result['steps'].append('Tạo đặt phòng bằng thao tác giao diện')
            books=get_json(backend,f'/reservations?propertyId={property_id}',token)
            b=next(x for x in books if x['fullName']==name)
            assert b['status']=='CONFIRMED','Booking không được lưu vào API/SQL'
            booking_row.get_by_role('button',name='Check-in').click()
            page.locator('.stay-card').filter(has_text=name).first.wait_for(state='visible')
            result['steps'].append('Check-in và đọc dữ liệu khách đang lưu trú')
            page.locator('.stay-card').filter(has_text=name).get_by_role('button',name='Mở hồ sơ & thanh toán →').click()
            page.get_by_role('button',name='＋ Thu tiền').click()
            form=page.locator('.modal')
            form.locator('.field:has(label:text("Phương thức")) select').select_option('TRANSFER')
            form.locator('input[placeholder="Mã tham chiếu ngân hàng"]').fill(name+'-PAY')
            form.get_by_role('button',name='Xác nhận thu tiền').click()
            page.locator('.modal .folio-summary').wait_for(state='visible')
            result['steps'].append('Thu đủ folio qua UI bằng chuyển khoản (UUID idempotencyKey)')
            page.get_by_role('button',name='Check-out →').click()
            page.locator('.stay-card').filter(has_text=name).wait_for(state='detached')
            result['steps'].append('Check-out qua giao diện Angular')
            books=get_json(backend,f'/reservations?propertyId={property_id}',token)
            b2=next(x for x in books if x['id']==b['id'])
            rooms=get_json(backend,f'/rooms?propertyId={property_id}',token)
            room=next(x for x in rooms if x['id']==b['roomId'])
            assert b2['status']=='CHECKED_OUT',f"Reservation chưa CHECKED_OUT: {b2['status']}"
            assert room['housekeepingStatus']=='DIRTY',f"Room chưa DIRTY: {room['housekeepingStatus']}"
            result['steps'].append('Đọc lại API/SQL: CHECKED_OUT và DIRTY đúng với thao tác UI')
            assert not errors,'Có JavaScript exception: '+str(errors)
            result['status']='PASS'
            result['bookingId']=b['id']
            page.screenshot(path=str(Path(__file__).parent/'angular_e2e_screenshot.png'),full_page=True)
            browser.close()
    except Exception as exc:
        result['status']='FAIL'
        result['errors'].append(str(exc))
    result['errors'].extend(errors)
    report.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False,indent=2))
    print('Bằng chứng:',report)
    return 0 if result['status']=='PASS' else 1


if __name__=='__main__':
    ap=argparse.ArgumentParser(description='Real Angular+API+SQL E2E on localhost DEMO (writes test data)')
    ap.add_argument('--frontend',default='http://localhost:4200')
    ap.add_argument('--backend',default='http://localhost:5080')
    ap.add_argument('--confirm-demo',action='store_true')
    args=ap.parse_args()
    for url in (args.frontend,args.backend):
        parsed=urllib.parse.urlparse(url)
        if parsed.scheme!='http' or parsed.hostname not in ('localhost','127.0.0.1'):
            sys.exit('Chỉ cho phép HTTP localhost, không chạy trên database thật')
    if not args.confirm_demo: sys.exit('Bộ test tạo giao dịch: cần thêm --confirm-demo trên DEMO DB')
    sys.exit(run(args.frontend,args.backend))
