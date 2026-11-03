import { useState, useEffect } from 'react'
import type { AuthUser } from '../roles/RoleDashboard'

const API_BASE_URL = 'http://localhost:5000'

interface CustomerProfile {
  customerId: number
  customerCode: string
  fullName: string
  phone: string
  email?: string
  addressLine?: string
  ward?: string
  district?: string
  province?: string
  notes?: string
}

interface CustomerDevice {
  deviceId: number
  modelName: string
  serialNumber?: string
  assetTag?: string
  cpu?: string
  ramDescription?: string
  storageDescription?: string
  operatingSystem?: string
  color?: string
  notes?: string
  createdAtUtc: string
}

interface CustomerTicket {
  repairTicketId: number
  ticketCode: string
  customerId: number
  deviceId: number
  deviceModel: string
  deviceSerial?: string
  issueDescription: string
  statusCode: string
  priority: string
  inspectionFee: number
  receivedAtUtc: string
  expectedCompletionAtUtc?: string
  actualCompletionAtUtc?: string
  closedAtUtc?: string
  assignedTechnician?: {
    userId: number
    userName: string
  }
}

interface CustomerAppointment {
  appointmentId: number
  appointmentCode: string
  scheduledAtUtc: string
  statusCode: string
  subject: string
  customerDescription?: string
  createdAtUtc: string
}

interface CustomerInvoice {
  invoiceId: number
  invoiceNumber: string
  invoiceType: string
  repairTicketId: number
  subTotal: number
  taxAmount: number
  discountAmount: number
  totalAmount: number
  paymentStatus: string
  issuedAtUtc?: string
  note?: string
}

interface CustomerPortalProps {
  user: AuthUser
  onLogout: () => void
}

export function CustomerPortal({ user, onLogout }: CustomerPortalProps) {
  const [activeTab, setActiveTab] = useState<'tickets' | 'devices' | 'appointments' | 'invoices' | 'profile'>('tickets')
  const [profile, setProfile] = useState<CustomerProfile | null>(null)
  const [devices, setDevices] = useState<CustomerDevice[]>([])
  const [tickets, setTickets] = useState<CustomerTicket[]>([])
  const [appointments, setAppointments] = useState<CustomerAppointment[]>([])
  const [invoices, setInvoices] = useState<CustomerInvoice[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // Selected ticket for detailed view
  const [selectedTicketId, setSelectedTicketId] = useState<number | null>(null)
  const [ticketDetail, setTicketDetail] = useState<any | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)

  // Edit Profile Form state
  const [editFullName, setEditFullName] = useState('')
  const [editEmail, setEditEmail] = useState('')
  const [editAddress, setEditAddress] = useState('')
  const [saveProfileSuccess, setSaveProfileSuccess] = useState(false)

  useEffect(() => {
    loadCustomerData()
  }, [])

  const loadCustomerData = async () => {
    setLoading(true)
    setError(null)
    const token = user.token || localStorage.getItem('tc_token')
    const headers = {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    }

    try {
      // 1. Profile
      const profRes = await fetch(`${API_BASE_URL}/api/v1/customer/profile`, { headers })
      if (profRes.ok) {
        const profData = await profRes.json()
        setProfile(profData)
        setEditFullName(profData.fullName || '')
        setEditEmail(profData.email || '')
        setEditAddress(profData.addressLine || '')
      }

      // 2. Devices
      const devRes = await fetch(`${API_BASE_URL}/api/v1/customer/devices`, { headers })
      if (devRes.ok) setDevices(await devRes.json())

      // 3. Tickets
      const tickRes = await fetch(`${API_BASE_URL}/api/v1/customer/tickets`, { headers })
      if (tickRes.ok) setTickets(await tickRes.json())

      // 4. Appointments
      const apptRes = await fetch(`${API_BASE_URL}/api/v1/customer/appointments`, { headers })
      if (apptRes.ok) setAppointments(await apptRes.json())

      // 5. Invoices
      const invRes = await fetch(`${API_BASE_URL}/api/v1/customer/invoices`, { headers })
      if (invRes.ok) setInvoices(await invRes.json())
    } catch {
      setError('Không thể kết nối đến máy chủ. Vui lòng thử lại.')
    } finally {
      setLoading(false)
    }
  }

  const loadTicketDetail = async (ticketId: number) => {
    setSelectedTicketId(ticketId)
    setDetailLoading(true)
    const token = user.token || localStorage.getItem('tc_token')
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/customer/tickets/${ticketId}`, {
        headers: { Authorization: `Bearer ${token}` },
      })
      if (res.ok) {
        setTicketDetail(await res.json())
      } else if (res.status === 403) {
        alert('Bạn không có quyền xem phiếu sửa chữa này.')
      }
    } catch {
      // fallback
    } finally {
      setDetailLoading(false)
    }
  }

  const handleUpdateProfile = async (e: React.FormEvent) => {
    e.preventDefault()
    const token = user.token || localStorage.getItem('tc_token')
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/customer/profile`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          fullName: editFullName,
          email: editEmail,
          addressLine: editAddress,
        }),
      })
      if (res.ok) {
        const updated = await res.json()
        setProfile(updated)
        setSaveProfileSuccess(true)
        setTimeout(() => setSaveProfileSuccess(false), 3000)
      }
    } catch {
      alert('Không thể lưu thông tin. Vui lòng thử lại.')
    }
  }

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(val)
  }

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'RECEIVED':
        return <span style={{ background: '#f4f4f5', color: '#18181b', padding: '3px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>ĐÃ TIẾP NHẬN</span>
      case 'DIAGNOSING':
        return <span style={{ background: '#eff6ff', color: '#1d4ed8', padding: '3px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>ĐANG CHẨN ĐOÁN</span>
      case 'QUOTED':
        return <span style={{ background: '#fefce8', color: '#854d0e', padding: '3px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>CHỜ DUYỆT BÁO GIÁ</span>
      case 'REPAIRING':
        return <span style={{ background: '#f5f3ff', color: '#6d28d9', padding: '3px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>ĐANG SỬA CHỮA</span>
      case 'COMPLETED':
      case 'RETURNED':
        return <span style={{ background: '#18181b', color: '#fff', padding: '3px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>HOÀN THÀNH</span>
      default:
        return <span style={{ background: '#f4f4f5', color: '#52525b', padding: '3px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>{status}</span>
    }
  }

  return (
    <div style={{ maxWidth: '1040px', margin: '24px auto', padding: '0 20px', fontFamily: 'inherit' }}>
      {/* Header Profile Summary */}
      <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '24px', marginBottom: '24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '6px' }}>CỔNG TỰ PHỤC VỤ KHÁCH HÀNG</div>
          <h2 style={{ fontSize: '22px', fontWeight: 700, margin: 0, color: 'var(--ink)' }}>
            Xin chào, {profile?.fullName || user.userName}
          </h2>
          <div style={{ fontSize: '13px', color: 'var(--sub)', marginTop: '6px', display: 'flex', gap: '16px' }}>
            <span>Số điện thoại: <strong>{profile?.phone || user.userName}</strong></span>
            {profile?.customerCode && <span>Mã khách hàng: <strong>{profile.customerCode}</strong></span>}
          </div>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <button
            type="button"
            className="secondary-btn"
            style={{ padding: '8px 16px', fontSize: '13px' }}
            onClick={() => setActiveTab('profile')}
          >
            Chỉnh sửa hồ sơ
          </button>
          <button
            type="button"
            className="secondary-btn"
            style={{ padding: '8px 16px', fontSize: '13px', color: '#dc2626' }}
            onClick={onLogout}
          >
            Đăng xuất
          </button>
        </div>
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', gap: '8px', borderBottom: '1px solid var(--line)', marginBottom: '24px' }}>
        <button
          type="button"
          onClick={() => { setActiveTab('tickets'); setSelectedTicketId(null) }}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            fontSize: '14px',
            fontWeight: activeTab === 'tickets' ? 600 : 400,
            color: activeTab === 'tickets' ? 'var(--ink)' : 'var(--sub)',
            borderBottom: activeTab === 'tickets' ? '2px solid var(--ink)' : '2px solid transparent',
            cursor: 'pointer',
          }}
        >
          Phiếu sửa chữa ({tickets.length})
        </button>
        <button
          type="button"
          onClick={() => { setActiveTab('devices'); setSelectedTicketId(null) }}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            fontSize: '14px',
            fontWeight: activeTab === 'devices' ? 600 : 400,
            color: activeTab === 'devices' ? 'var(--ink)' : 'var(--sub)',
            borderBottom: activeTab === 'devices' ? '2px solid var(--ink)' : '2px solid transparent',
            cursor: 'pointer',
          }}
        >
          Máy của tôi ({devices.length})
        </button>
        <button
          type="button"
          onClick={() => { setActiveTab('appointments'); setSelectedTicketId(null) }}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            fontSize: '14px',
            fontWeight: activeTab === 'appointments' ? 600 : 400,
            color: activeTab === 'appointments' ? 'var(--ink)' : 'var(--sub)',
            borderBottom: activeTab === 'appointments' ? '2px solid var(--ink)' : '2px solid transparent',
            cursor: 'pointer',
          }}
        >
          Lịch hẹn ({appointments.length})
        </button>
        <button
          type="button"
          onClick={() => { setActiveTab('invoices'); setSelectedTicketId(null) }}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            fontSize: '14px',
            fontWeight: activeTab === 'invoices' ? 600 : 400,
            color: activeTab === 'invoices' ? 'var(--ink)' : 'var(--sub)',
            borderBottom: activeTab === 'invoices' ? '2px solid var(--ink)' : '2px solid transparent',
            cursor: 'pointer',
          }}
        >
          Hóa đơn ({invoices.length})
        </button>
      </div>

      {loading && (
        <div style={{ textAlign: 'center', padding: '40px', color: 'var(--sub)' }}>
          Đang tải dữ liệu hồ sơ của bạn...
        </div>
      )}

      {error && (
        <div style={{ padding: '16px', background: '#fef2f2', border: '1px solid #f87171', borderRadius: '8px', color: '#dc2626', fontSize: '13px', marginBottom: '20px' }}>
          {error}
        </div>
      )}

      {/* 1. TICKETS TAB */}
      {!loading && activeTab === 'tickets' && (
        <div>
          {detailLoading && (
            <div style={{ textAlign: 'center', padding: '20px', color: 'var(--sub)' }}>
              Đang tải chi tiết phiếu...
            </div>
          )}
          {selectedTicketId && ticketDetail ? (
            <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '24px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <div>
                  <button
                    type="button"
                    onClick={() => setSelectedTicketId(null)}
                    style={{ background: 'none', border: 'none', fontSize: '13px', color: 'var(--sub)', cursor: 'pointer', marginBottom: '6px' }}
                  >
                    ← Quay lại danh sách phiếu
                  </button>
                  <h3 style={{ fontSize: '18px', fontWeight: 600, margin: 0 }}>
                    Chi tiết phiếu: {ticketDetail.ticketCode}
                  </h3>
                </div>
                {getStatusBadge(ticketDetail.statusCode)}
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '20px', fontSize: '13.5px', marginTop: '16px' }}>
                <div style={{ background: '#fafafa', padding: '16px', borderRadius: '8px', border: '1px solid var(--line)' }}>
                  <h4 style={{ fontSize: '13px', fontWeight: 600, marginBottom: '10px', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Thông tin thiết bị</h4>
                  <div>Model: <strong>{ticketDetail.device?.modelName || 'Chưa cập nhật'}</strong></div>
                  <div>Serial/Service Tag: <strong>{ticketDetail.device?.serialNumber || 'N/A'}</strong></div>
                  <div>CPU: <strong>{ticketDetail.device?.cpu || 'N/A'}</strong></div>
                  <div>RAM: <strong>{ticketDetail.device?.ramDescription || 'N/A'}</strong></div>
                  <div>Ổ cứng: <strong>{ticketDetail.device?.storageDescription || 'N/A'}</strong></div>
                </div>

                <div style={{ background: '#fafafa', padding: '16px', borderRadius: '8px', border: '1px solid var(--line)' }}>
                  <h4 style={{ fontSize: '13px', fontWeight: 600, marginBottom: '10px', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Tình trạng sửa chữa</h4>
                  <div>Mô tả lỗi: <strong>{ticketDetail.issueDescription}</strong></div>
                  <div>Tình trạng tiếp nhận: <strong>{ticketDetail.initialCondition || 'Bình thường'}</strong></div>
                  <div>Phí kiểm tra: <strong>{formatCurrency(ticketDetail.inspectionFee)}</strong></div>
                  <div>Kỹ thuật viên phụ trách: <strong>{ticketDetail.assignedTechnician?.userName || 'Đang sắp xếp'}</strong></div>
                </div>
              </div>

              {/* Waybill / Shipping Timeline if available */}
              {ticketDetail.shipments && ticketDetail.shipments.length > 0 && (
                <div style={{ marginTop: '24px' }}>
                  <h4 style={{ fontSize: '14px', fontWeight: 600, marginBottom: '12px' }}>Vận đơn giao nhận (GHN)</h4>
                  {ticketDetail.shipments.map((ship: any) => (
                    <div key={ship.shipmentId} style={{ border: '1px solid var(--line)', borderRadius: '8px', padding: '16px', marginBottom: '12px' }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', marginBottom: '8px' }}>
                        <span>Mã vận đơn: <strong>{ship.waybillCode}</strong> ({ship.direction === 'INBOUND' ? 'Khách gửi đến' : 'Tiệm trả về'})</span>
                        <span style={{ fontWeight: 600 }}>{ship.statusCode}</span>
                      </div>
                      <div style={{ fontSize: '12.5px', color: 'var(--sub)' }}>
                        Nơi gửi: {ship.senderAddress} → Nơi nhận: {ship.receiverAddress}
                      </div>

                      {/* Timeline Events */}
                      {ship.events && ship.events.length > 0 && (
                        <div style={{ marginTop: '12px', paddingLeft: '12px', borderLeft: '2px solid var(--line)' }}>
                          {ship.events.map((ev: any) => (
                            <div key={ev.eventId} style={{ marginBottom: '8px', fontSize: '12px' }}>
                              <span style={{ color: 'var(--sub)' }}>{new Date(ev.occurredAtUtc).toLocaleString('vi-VN')}</span>: <strong>{ev.description || ev.providerStatus}</strong> {ev.locationText && `(${ev.locationText})`}
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>
          ) : tickets.length === 0 ? (
            <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '40px', textAlign: 'center', color: 'var(--sub)' }}>
              Bạn chưa có phiếu sửa chữa nào trong hệ thống.
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {tickets.map((t) => (
                <div
                  key={t.repairTicketId}
                  onClick={() => loadTicketDetail(t.repairTicketId)}
                  style={{
                    background: '#fff',
                    border: '1px solid var(--line)',
                    borderRadius: '10px',
                    padding: '18px 20px',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    cursor: 'pointer',
                    transition: 'border-color 0.15s ease',
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.borderColor = 'var(--ink)')}
                  onMouseLeave={(e) => (e.currentTarget.style.borderColor = 'var(--line)')}
                >
                  <div>
                    <div style={{ display: 'flex', gap: '10px', alignItems: 'center', marginBottom: '4px' }}>
                      <span style={{ fontWeight: 700, fontSize: '15px' }}>{t.ticketCode}</span>
                      {getStatusBadge(t.statusCode)}
                    </div>
                    <div style={{ fontSize: '13px', color: 'var(--ink)', marginBottom: '2px' }}>
                      {t.deviceModel} {t.deviceSerial ? `(${t.deviceSerial})` : ''} — <em>{t.issueDescription}</em>
                    </div>
                    <div style={{ fontSize: '12px', color: 'var(--sub)' }}>
                      Tiếp nhận: {new Date(t.receivedAtUtc).toLocaleDateString('vi-VN')} · Kỹ thuật viên: {t.assignedTechnician?.userName || 'Đang sắp xếp'}
                    </div>
                  </div>

                  <div style={{ textAlign: 'right', fontSize: '13px' }}>
                    <span style={{ color: 'var(--sub)', fontSize: '12px' }}>Xem chi tiết</span> <span>→</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* 2. DEVICES TAB */}
      {!loading && activeTab === 'devices' && (
        <div>
          {devices.length === 0 ? (
            <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '40px', textAlign: 'center', color: 'var(--sub)' }}>
              Chưa có thiết bị nào được liên kết với số điện thoại này.
            </div>
          ) : (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: '16px' }}>
              {devices.map((d) => (
                <div key={d.deviceId} style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '10px', padding: '20px' }}>
                  <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '4px' }}>THIẾT BỊ ĐÃ ĐĂNG KÝ</div>
                  <h4 style={{ fontSize: '16px', fontWeight: 600, margin: '0 0 10px 0' }}>{d.modelName}</h4>
                  <div style={{ fontSize: '12.5px', color: 'var(--sub)', lineHeight: 1.6 }}>
                    <div>Serial/SN: <strong>{d.serialNumber || 'Chưa ghi nhận'}</strong></div>
                    {d.cpu && <div>CPU: {d.cpu}</div>}
                    {d.ramDescription && <div>RAM: {d.ramDescription}</div>}
                    {d.storageDescription && <div>Ổ cứng: {d.storageDescription}</div>}
                    {d.operatingSystem && <div>Hệ điều hành: {d.operatingSystem}</div>}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* 3. APPOINTMENTS TAB */}
      {!loading && activeTab === 'appointments' && (
        <div>
          {appointments.length === 0 ? (
            <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '40px', textAlign: 'center', color: 'var(--sub)' }}>
              Bạn chưa có lịch hẹn đặt trước nào.
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {appointments.map((a) => (
                <div key={a.appointmentId} style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '10px', padding: '16px 20px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div>
                    <div style={{ fontWeight: 600, fontSize: '14px', marginBottom: '4px' }}>
                      {a.appointmentCode} — {a.subject}
                    </div>
                    <div style={{ fontSize: '12.5px', color: 'var(--sub)' }}>
                      Giờ hẹn: <strong>{new Date(a.scheduledAtUtc).toLocaleString('vi-VN')}</strong> · Trạng thái: {a.statusCode}
                    </div>
                    {a.customerDescription && (
                      <div style={{ fontSize: '12px', color: '#666', marginTop: '4px' }}>
                        Ghi chú: {a.customerDescription}
                      </div>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* 4. INVOICES TAB */}
      {!loading && activeTab === 'invoices' && (
        <div>
          {invoices.length === 0 ? (
            <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '40px', textAlign: 'center', color: 'var(--sub)' }}>
              Chưa có hóa đơn nào được phát hành.
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {invoices.map((i) => (
                <div key={i.invoiceId} style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '10px', padding: '16px 20px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div>
                    <div style={{ fontWeight: 600, fontSize: '14px', marginBottom: '4px' }}>
                      Số HĐ: {i.invoiceNumber}
                    </div>
                    <div style={{ fontSize: '12.5px', color: 'var(--sub)' }}>
                      Ngày lập: {i.issuedAtUtc ? new Date(i.issuedAtUtc).toLocaleDateString('vi-VN') : 'Mới tạo'} · Trạng thái: <strong>{i.paymentStatus}</strong>
                    </div>
                  </div>
                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontWeight: 700, fontSize: '16px' }}>{formatCurrency(i.totalAmount)}</div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* 5. PROFILE TAB */}
      {!loading && activeTab === 'profile' && (
        <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '28px', maxWidth: '600px' }}>
          <h3 style={{ fontSize: '18px', fontWeight: 600, marginBottom: '16px' }}>Cập nhật thông tin cá nhân</h3>
          {saveProfileSuccess && (
            <div style={{ background: '#ecfdf5', color: '#065f46', padding: '10px 14px', borderRadius: '6px', fontSize: '13px', marginBottom: '16px' }}>
              ✓ Đã lưu thông tin hồ sơ thành công!
            </div>
          )}
          <form onSubmit={handleUpdateProfile} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
            <div>
              <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 600, marginBottom: '4px' }}>Họ và tên</label>
              <input
                type="text"
                value={editFullName}
                onChange={(e) => setEditFullName(e.target.value)}
                style={{ width: '100%', padding: '10px', border: '1px solid var(--line)', borderRadius: '6px', fontSize: '13.5px' }}
                required
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 600, marginBottom: '4px' }}>Email</label>
              <input
                type="email"
                value={editEmail}
                onChange={(e) => setEditEmail(e.target.value)}
                style={{ width: '100%', padding: '10px', border: '1px solid var(--line)', borderRadius: '6px', fontSize: '13.5px' }}
                placeholder="example@gmail.com"
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 600, marginBottom: '4px' }}>Địa chỉ nhận máy tận nơi</label>
              <input
                type="text"
                value={editAddress}
                onChange={(e) => setEditAddress(e.target.value)}
                style={{ width: '100%', padding: '10px', border: '1px solid var(--line)', borderRadius: '6px', fontSize: '13.5px' }}
                placeholder="Số nhà, tên đường, phường/xã..."
              />
            </div>
            <div style={{ marginTop: '10px' }}>
              <button type="submit" className="button" style={{ padding: '10px 24px' }}>
                Lưu thay đổi
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  )
}
