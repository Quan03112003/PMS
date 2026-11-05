#!/usr/bin/env python3
"""LOTUS PMS V3 integration test runner - DOES MUTATE LOCAL DEMO DATABASE.
Run on the user's Windows PC AFTER the Angular/.NET/SQL Server stack is started.
Never use against production. Standard library only, Python 3.10+.
"""
from __future__ import annotations
import argparse
import datetime as dt
import json
import pathlib
import sys
import uuid
from concurrent.futures import ThreadPoolExecutor
import urllib.error
import urllib.parse
import urllib.request

class Failure(Exception):
    pass

class API:
    def __init__(self, base):
        self.base = base.rstrip('/') + '/api'
        self.token = None
    def request(self, method, path, data=None, expected=200):
        url = self.base + path
        payload = json.dumps(data, ensure_ascii=False).encode('utf-8') if data is not None else None
        headers = {'Accept': 'application/json'}
        if payload is not None:
            headers['Content-Type'] = 'application/json; charset=utf-8'
        if self.token:
            headers['Authorization'] = 'Bearer ' + self.token
        req = urllib.request.Request(url, data=payload, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=15) as r:
                code, raw = r.status, r.read().decode('utf-8-sig')
        except urllib.error.HTTPError as e:
            code, raw = e.code, e.read().decode('utf-8-sig', errors='replace')
        except urllib.error.URLError as e:
            raise Failure(f'Không kết nối được tới {url}: {e.reason}') from e
        if code != expected:
            raise Failure(f'{method} {path}: HTTP {code}, expected={expected}, body={raw[:350]}')
        try:
            return json.loads(raw) if raw else None
        except json.JSONDecodeError:
            return raw
    def get(self, path, expected=200): return self.request('GET', path, expected=expected)
    def post(self, path, data, expected=200): return self.request('POST', path, data, expected)
    def patch(self, path, data, expected=200): return self.request('PATCH', path, data, expected)

results = []
def check(id_, title, fn):
    try:
        detail=fn()
        results.append({'id':id_, 'status':'PASS', 'title':title, 'detail':str(detail or '')})
        print(f'PASS  {id_} {title}')
        return detail
    except Exception as e:
        results.append({'id':id_, 'status':'FAIL', 'title':title, 'detail':str(e)})
        print(f'FAIL  {id_} {title}: {e}')
        raise Failure(str(e)) from e

def verify(condition, message):
    if not condition: raise Failure(message)

def phase(name, fn):
    print('\n## ' + name)
    try:
        fn()
    except Failure as e:
        results.append({'id':'PHASE-BLOCKED', 'status':'BLOCKED', 'title':name, 'detail':str(e)})
        print(f'BLOCKED: các phép thử phụ thuộc trong {name} không thể tiếp tục: {e}')

def run(base, out):
    api=API(base)
    tag='E2E-'+uuid.uuid4().hex[:8].upper()
    property_id=None
    def key(): return str(uuid.uuid4())
    def setup():
        nonlocal property_id
        health=check('API-01', 'Health endpoint',lambda: api.get('/health'))
        verify(health.get('status')=='ok','Health status không phải ok')
        login=check('API-02','Đăng nhập admin nhận JWT',lambda: api.post('/auth/login', {'email':'admin@pms.local','password':'Demo@12345'}))
        verify(bool(login.get('token')),'Thiếu token');api.token=login['token']
        properties=check('API-03','Đọc danh sách khách sạn',lambda: api.get('/properties'))
        verify(len(properties)>0,'Chưa có khách sạn seed');property_id=properties[0]['id']
        print(f'PropertyId={property_id}; TestTag={tag}')
    phase('Kết nối & xác thực',setup)
    if not property_id:
        save_results(out, base, tag)
        return 2

    today=dt.date.today()
    arrival=today.isoformat()
    checkout=(today+dt.timedelta(days=2)).isoformat()
    def available_rooms():
        return api.get(f'/availability?propertyId={property_id}&startDate={arrival}&endDate={checkout}')
    def booking(room, suffix='ONE'):
        return api.post('/reservations', {'propertyId':property_id,'roomId':room['id'],'guestName':f'{tag}-{suffix}',
            'phone':'0900000000','email':None,'checkInDate':arrival,'checkOutDate':checkout,
            'adults':1,'children':0,'nightlyRate':room['baseRate'],'bookingSource':'DIRECT',
            'groupCode':None,'note':f'{tag}: chỉ dùng demo'})

    def single():
        available=check('API-04','Tìm phòng khả dụng',available_rooms)
        verify(len(available)>0,'Không có phòng sẵn sàng để test')
        room=available[0]
        b=check('API-05','Tạo đặt phòng lẻ',lambda: booking(room))
        verify(b['status']=='CONFIRMED','Booking chưa CONFIRMED')
        def no_overlap():
            api.post('/reservations',{'propertyId':property_id,'roomId':room['id'],'guestName':f'{tag}-DUP','phone':'0900000000',
               'checkInDate':arrival,'checkOutDate':checkout,'adults':1,'children':0,'nightlyRate':room['baseRate']},409)
        check('API-06','Chặn booking trùng lịch (HTTP 409)',no_overlap)
        s=check('API-07','Check-in',lambda: api.post(f"/reservations/{b['id']}/check-in", {}))
        stay_id=s['id']
        def cannot_checkout():
            api.post(f'/stays/{stay_id}/checkout',{},409)
        check('API-08','Chặn check-out khi chưa trả đủ',cannot_checkout)
        services=check('API-09','Lấy dịch vụ',lambda: api.get(f'/services?propertyId={property_id}'))
        inv=next((x for x in services if x['trackInventory'] and x['stockQuantity']>=1 and x['price']>0),None)
        if inv:
            c=check('API-10','Thêm minibar và trừ tồn kho',lambda: api.post(f'/stays/{stay_id}/services',{'serviceId':inv['id'],'quantity':1}))
            verify(c['amount'] == inv['price'], 'Tiền dịch vụ không khớp')
            after=api.get(f'/services?propertyId={property_id}')
            new=next(x for x in after if x['id']==inv['id'])
            verify(new['stockQuantity']==inv['stockQuantity']-1,'Tồn kho không giảm chính xác')
        else:
            print('SKIP API-10 Không có hàng theo dõi tồn kho còn hàng >0')
            results.append({'id':'API-10','status':'SKIPPED','title':'Thêm minibar và trừ kho','detail':'Không có mặt hàng seed phù hợp'})
        folio=check('API-11','Đọc folio và số dư',lambda: api.get(f'/stays/{stay_id}/folio'))
        verify(folio['balance']>0,'Folio không có số phải thu')
        payment_key=key()
        payment_data={'amount':folio['balance'],'method':'TRANSFER','reference':tag,'idempotencyKey':payment_key}
        payment=check('API-12','Thu tiền chuyển khoản đủ',lambda: api.post(f'/stays/{stay_id}/payments',payment_data))
        verify(payment['amount']==folio['balance'],'Số tiền thanh toán không khớp')
        def replay_payment():
            old=api.get(f'/stays/{stay_id}/folio')
            again=api.post(f'/stays/{stay_id}/payments',payment_data)
            new=api.get(f'/stays/{stay_id}/folio')
            verify(again['id']==payment['id'] and len(old['payments'])==len(new['payments']), 'Request thử lại tạo thanh toán trùng')
        check('V3-01','Thử lại cùng idempotencyKey không ghi trùng',replay_payment)
        folio_after=api.get(f'/stays/{stay_id}/folio')
        verify(folio_after['balance']==0,'Folio còn tiền')
        check('API-13','Checkout thành công',lambda: api.post(f'/stays/{stay_id}/checkout',{}))
        after=api.get(f'/rooms?propertyId={property_id}')
        rm=next(x for x in after if x['id']==room['id'])
        verify(rm['housekeepingStatus']=='DIRTY','Phòng sau checkout không chuyển DIRTY')
        def future_dirty_booking():
            future=(today+dt.timedelta(days=5)).isoformat()
            later=(today+dt.timedelta(days=6)).isoformat()
            created=api.post('/reservations',{'propertyId':property_id,'roomId':room['id'],'guestName':tag+'-FUTURE',
                'phone':'0000000000','checkInDate':future,'checkOutDate':later,'adults':1,'children':0,'nightlyRate':room['baseRate']})
            verify(created['status']=='CONFIRMED','Không đặt tương lai được trên phòng DIRTY')
        check('V3-02','Đặt phòng tương lai dù phòng đang DIRTY',future_dirty_booking)
        def restore_blocked_by_new_booking():
            # A is checked out, but a NEW confirmed reservation occupies the remaining part of A's old date range.
            tomorrow=(today+dt.timedelta(days=1)).isoformat()
            blocking=api.post('/reservations',{'propertyId':property_id,'roomId':room['id'],'guestName':tag+'-NEWBOOKING',
              'phone':'0000000000','checkInDate':tomorrow,'checkOutDate':checkout,
              'adults':1,'children':0,'nightlyRate':room['baseRate']})
            verify(blocking['status']=='CONFIRMED','Không tạo được booking mới cho tình huống khôi phục')
            api.post(f'/stays/{stay_id}/restore',{},409)
        check('V3-13','Không khôi phục checkout khi có booking mới chồng lịch',restore_blocked_by_new_booking)
        check('API-14','Dọn phòng và duyệt phòng',lambda: (api.patch(f"/rooms/{room['id']}/housekeeping",{'status':'CLEAN'}),api.patch(f"/rooms/{room['id']}/housekeeping",{'status':'INSPECTED'})))
        check('API-15','Chặn checkout lặp',cannot_checkout)
    phase('Giai đoạn 1: booking lẻ → check-in → minibar → thanh toán → checkout',single)

    def group():
        rooms=available_rooms();verify(len(rooms)>=2,'Cần ít nhất 2 phòng trống cho đoàn')
        g=check('API-16','Đặt đoàn 2 phòng',lambda: api.post('/group-bookings',{'propertyId':property_id,
             'guestName':f'{tag}-GROUP','phone':'0900000000','checkInDate':arrival,'checkOutDate':checkout,
             'roomIds':[r['id'] for r in rooms[:2]],'bookingSource':'DIRECT','note':tag}))
        code=g['groupCode']
        group_rooms=check('V3-03','Đọc danh sách phòng đoàn',lambda: api.get(f'/groups/{urllib.parse.quote(code)}/occupants?propertyId={property_id}'))
        verify(len(group_rooms)==2,'Không trả đủ các phòng trong đoàn')
        for i, gr in enumerate(group_rooms):
            check(f'V3-04-{i}','Gán người ở riêng cho phòng đoàn',lambda gr=gr,i=i: api.post(f"/reservations/{gr['id']}/occupants",
                {'fullName':f'{tag}-GUEST-{i}','phone':'0900111000','nationality':'VN','identityLast4':'1234','isPrimary':True}))
        registered=api.get(f'/groups/{urllib.parse.quote(code)}/occupants?propertyId={property_id}')
        verify(all(len(x['occupants'])==1 for x in registered),'Phân phòng đoàn không giữ danh sách người ở độc lập')
        check('API-17','Nhận phòng đoàn',lambda: api.post(f'/groups/{urllib.parse.quote(code)}/check-in?propertyId={property_id}',{}))
        booking_list=api.get(f'/reservations?propertyId={property_id}')
        rs=[x for x in booking_list if x.get('groupCode')==code and x['status']=='CHECKED_IN']
        verify(len(rs)==2,'Không có đủ 2 booking đang ở trong đoàn')
        # Group payable = money owed; read all in-house stay folios.
        stays=api.get(f'/stays?propertyId={property_id}')
        selected=[x for x in stays if x['reservationId'] in {r['id'] for r in rs}]
        verify(len(selected)==2,'Không có đủ 2 stay trong đoàn')
        total=sum(api.get(f"/stays/{s['id']}/folio")['balance'] for s in selected)
        verify(total>0,'Tiền đoàn phải >0')
        group_payment={'propertyId':property_id,'amount':total,'method':'TRANSFER','idempotencyKey':key()}
        check('API-18','Thanh toán hộ cả đoàn',lambda: api.post(f'/groups/{urllib.parse.quote(code)}/pay',group_payment))
        check('V3-05','Không thu đoàn trùng khi gửi lại key',lambda: api.post(f'/groups/{urllib.parse.quote(code)}/pay',group_payment))
        check('API-19','Checkout đoàn',lambda: api.post(f'/groups/{urllib.parse.quote(code)}/checkout?propertyId={property_id}',{}))
        detail=api.get(f'/reservations?propertyId={property_id}')
        verify(sum(x['status']=='CHECKED_OUT' for x in detail if x.get('groupCode')==code)==2,'Đoàn chưa checkout đủ')
    phase('Giai đoạn 1: đặt đoàn và thanh toán hộ',group)

    def waitlist():
        rooms=available_rooms(); verify(len(rooms)>0,'Không còn phòng cho hàng đợi')
        w=check('API-20','Thêm yêu cầu vào hàng đợi',lambda: api.post('/waitlist',{'propertyId':property_id,
            'guestName':f'{tag}-WAIT','phone':'0900000000','checkInDate':arrival,'checkOutDate':checkout,'roomTypeId':rooms[0]['roomTypeId'],'note':tag}))
        assignment=check('API-21','Gán phòng từ hàng đợi',lambda: api.post(f"/waitlist/{w['id']}/assign",{'roomId':rooms[0]['id']}))
        verify(assignment.get('bookingId', 0)>0 and assignment.get('waitlistId')==w['id'],'Không nhận được liên kết booking/waitlist chính xác')
    phase('Giai đoạn 1: hàng đợi',waitlist)

    def debt():
        rooms=available_rooms();verify(len(rooms)>0,'Không còn phòng cho test công nợ')
        b=booking(rooms[0],'DEBT')
        s=check('API-22','Check-in khách test công nợ',lambda: api.post(f"/reservations/{b['id']}/check-in",{}))
        d=check('API-23','Checkout ghi nhận công nợ',lambda: api.post(f"/stays/{s['id']}/checkout-debt",{'note':tag}))
        verify(d['outstandingAmount']>0,'Không ghi nhận dư nợ')
        amount=d['outstandingAmount']
        debt_payment={'amount':amount,'method':'TRANSFER','reference':tag,'idempotencyKey':key()}
        check('API-24','Thu hết công nợ',lambda: api.post(f"/debts/{d['id']}/collect",debt_payment))
        check('V3-06','Thử lại thu nợ không ghi trùng',lambda: api.post(f"/debts/{d['id']}/collect",debt_payment))
        debts=api.get(f'/debts?propertyId={property_id}')
        item=next(x for x in debts if x['id']==d['id'])
        verify(item['outstandingAmount']==0 and item['status']=='SETTLED','Công nợ chưa tất toán')
    phase('Giai đoạn 2: công nợ và thu nợ',debt)

    def cancellation_refund():
        rooms=available_rooms();verify(len(rooms)>0,'Không còn phòng trống để test hoàn cọc')
        b=booking(rooms[0],'REFUND')
        deposit_data={'amount':50000,'method':'TRANSFER','reference':tag+'-DEP','idempotencyKey':key()}
        check('V3-07','Nhận cọc 50.000',lambda: api.post(f"/reservations/{b['id']}/deposit",deposit_data))
        check('V3-08','Chặn hủy khi còn cọc',lambda: api.post(f"/reservations/{b['id']}/cancel",{},409))
        refund_data={'amount':50000,'method':'TRANSFER','reference':tag+'-REFUND','idempotencyKey':key()}
        refund=check('V3-09','Hoàn cọc có chứng từ âm',lambda: api.post(f"/reservations/{b['id']}/refund-deposit",refund_data))
        verify(refund['amount']==-50000 and refund['kind']=='REFUND','Giá trị hoặc loại hoàn cọc sai')
        def replay_refund():
            r=api.post(f"/reservations/{b['id']}/refund-deposit",refund_data)
            verify(r['id']==refund['id'],'Gửi lại hoàn tiền tạo chứng từ mới')
        check('V3-10','Thử lại hoàn cọc không ghi trùng',replay_refund)
        check('V3-11','Được hủy sau khi hoàn hết cọc',lambda: api.post(f"/reservations/{b['id']}/cancel",{}))
    phase('V3: hoàn cọc – hủy booking',cancellation_refund)

    def booking_race():
        rooms=available_rooms();verify(len(rooms)>0,'Thiếu phòng để test cạnh tranh booking')
        room=rooms[0]
        def task(i):
            try:
                api.post('/reservations', {'propertyId':property_id,'roomId':room['id'],
                    'guestName':f'{tag}-BOOKRACE-{i}','phone':'0000000000',
                    'checkInDate':arrival,'checkOutDate':checkout,'adults':1,'children':0,'nightlyRate':room['baseRate']})
                return 200
            except Failure as e:
                if 'HTTP 409' in str(e):return 409
                raise
        def race():
            with ThreadPoolExecutor(max_workers=2) as pool:
                statuses=list(pool.map(task,[1,2]))
            verify(sorted(statuses)==[200,409],f'Tạo trùng khi cạnh tranh booking: {statuses}')
        check('V3-14','Đặt cùng phòng đồng thời chỉ nhận một booking',race)
    phase('V3: giao dịch đặt phòng đồng thời',booking_race)

    def concurrent():
        rooms=available_rooms();verify(len(rooms)>0,'Không còn phòng để test giao dịch cạnh tranh')
        b=booking(rooms[0],'RACE')
        st=api.post(f"/reservations/{b['id']}/check-in",{})
        folio=api.get(f"/stays/{st['id']}/folio");total=folio['balance']
        verify(total>0,'Folio không có dư nợ')
        def pay(_):
            try:
                api.post(f"/stays/{st['id']}/payments",{'amount':total,'method':'TRANSFER',
                  'reference':tag+'-RACE','idempotencyKey':key()})
                return 200
            except Failure as e:
                if 'HTTP 409' in str(e):return 409
                raise
        def race():
            with ThreadPoolExecutor(max_workers=2) as pool:
                outcomes=list(pool.map(pay,range(2)))
            verify(sorted(outcomes)==[200,409],f'Race kỳ vọng 1x200 1x409; thực tế {outcomes}')
            after=api.get(f"/stays/{st['id']}/folio")
            verify(after['balance']==0 and len(after['payments'])==1,'Lỗi ghi tiền trùng hoặc sai số dư sau race')
        check('V3-12','2 request đồng thời: chỉ 1 thanh toán được ghi',race)
    phase('V3: giao dịch đồng thời',concurrent)

    def mgmt():
        check('API-25','Báo cáo toàn chuỗi',lambda: api.get(f'/reports/chain?from={arrival}&to={arrival}'))
        check('API-26','Kiểm tra lịch sử kho',lambda: api.get(f'/stock/movements?propertyId={property_id}'))
        voucher=check('API-27','Phiếu chi chuyển khoản',lambda: api.post('/vouchers',{'propertyId':property_id,
            'type':'EXPENSE','method':'TRANSFER','amount':15000,'category':'E2E_TEST','description':tag}))
        verify(voucher['amount']==15000,'Sai tiền phiếu chi')
        check('API-28','Kiểm tra báo cáo đại lý',lambda: api.get(f'/reports/agencies?propertyId={property_id}&from={arrival}&to={arrival}'))
        resp=api.post('/auth/login',{'email':'reception@pms.local','password':'Demo@12345'})
        admin_token=api.token
        front_property=resp['user']['propertyId']
        try:
            api.token=resp['token']
            def denied():
                api.post('/rate-plans',{'propertyId':front_property,'code':'BLOCKED-'+tag,'name':'Không được tạo',
                  'mode':'OVERNIGHT','basePrice':1,'includedHours':1,'extraHourPrice':0},expected=403)
            check('API-29','Lễ tân bị từ chối sửa bảng giá (403)',denied)
        finally:api.token=admin_token
    phase('Giai đoạn 2 & 3: chứng từ, phân quyền, báo cáo',mgmt)

    save_results(out,base,tag)
    failed=sum(x['status']=='FAIL' for x in results)
    blocked=sum(x['status']=='BLOCKED' for x in results)
    print(f'\nTỔNG KẾT: {len(results)} case: {sum(x["status"]=="PASS" for x in results)} PASS, {failed} FAIL, {blocked} BLOCKED, {sum(x["status"]=="SKIPPED" for x in results)} SKIPPED')
    print('JSON:',out)
    return 1 if (failed or blocked) else 0

def save_results(out,base,tag):
    pathlib.Path(out).write_text(json.dumps({'baseUrl':base,'tag':tag,'dateUtc':dt.datetime.now(dt.timezone.utc).isoformat(), 'cases':results},ensure_ascii=False,indent=2),encoding='utf-8')

if __name__=='__main__':
    ap=argparse.ArgumentParser(description='LOTUS PMS V3 - kiểm thử API E2E với database DEMO. Có ghi dữ liệu thử.')
    ap.add_argument('--url',default='http://localhost:5080',help='Chỉ cho phép localhost để tránh ghi vào production')
    ap.add_argument('--out',default='pms_v3_api_e2e_results.json')
    ap.add_argument('--confirm-demo',action='store_true',help='Xác nhận đây là database test, chấp nhận tạo giao dịch mẫu')
    args=ap.parse_args()
    url=urllib.parse.urlparse(args.url)
    if url.hostname not in {'127.0.0.1','localhost'}:
        sys.exit('Chỉ chạy test trên localhost. Không được ghi dữ liệu giả vào server thật.')
    if not args.confirm_demo:
        sys.exit('Bộ test có ghi dữ liệu. Chỉ chạy trên database DEMO: thêm --confirm-demo')
    sys.exit(run(args.url,args.out))
