import { useState, useEffect } from 'react'
import { KnowledgeAdmin } from '../admin/KnowledgeAdmin'
import { SupportChatInbox } from '../support/SupportChatInbox'
import { CustomerPortal } from '../customer/CustomerPortal'
import { API_BASE_URL } from '../../api/client'

export type UserRole = 'ADMIN' | 'RECEPTIONIST' | 'TECHNICIAN' | 'INVENTORY' | 'CASHIER' | 'SUPPORT' | 'CUSTOMER'

export interface AuthUser {
  userId: number
  userName: string
  email?: string
  branchId?: number | null
  roles: string[]
  permissions: string[]
  token: string
}

interface RoleDashboardProps {
  user: AuthUser
  initialPath: string
  onLogout: () => void
  onNavigateHome: () => void
}

export function RoleDashboard({ user, initialPath, onLogout, onNavigateHome }: RoleDashboardProps) {
  const [currentPath, setCurrentPath] = useState(initialPath)

  // Sub-tabs depending on current portal
  const isAdmin = user.roles.includes('ADMIN')
  const isReception = isAdmin || user.roles.includes('RECEPTIONIST')
  const isTech = isAdmin || user.roles.includes('TECHNICIAN')
  const isInventory = isAdmin || user.roles.includes('INVENTORY')
  const isCashier = isAdmin || user.roles.includes('CASHIER')
  const isSupport = isAdmin || user.roles.includes('SUPPORT')

  const [activeAdminSubTab, setActiveAdminSubTab] = useState<'KNOWLEDGE' | 'USERS' | 'AUDIT' | 'INTEGRATIONS'>('KNOWLEDGE')
  
  // Third-party integrations state (Phase 6)
  const [integrations, setIntegrations] = useState<{ smtpConfigured: boolean; mode: string; ghnConfigured: boolean } | null>(null)
  
  // Technicians management for admin (Phase 6B)
  const [adminTechs, setAdminTechs] = useState<any[]>([])
  const [savingTechId, setSavingTechId] = useState<number | null>(null)
  const [techMsg, setTechMsg] = useState<string | null>(null)

  useEffect(() => {
    fetch(`${API_BASE_URL}/api/v1/integrations/status`)
      .then((r) => (r.ok ? r.json() : null))
      .then((data) => {
        if (data) setIntegrations(data)
      })
      .catch(() => {})

    fetch(`${API_BASE_URL}/api/v1/technicians/public`)
      .then((r) => (r.ok ? r.json() : []))
      .then((data) => {
        if (Array.isArray(data)) setAdminTechs(data)
      })
      .catch(() => {})
  }, [])

  const handleUpdateTechProfile = async (tech: any) => {
    setSavingTechId(tech.userId)
    setTechMsg(null)
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/technicians/${tech.userId}/profile`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${user.token || localStorage.getItem('tc_token')}`,
        },
        body: JSON.stringify({
          displayName: tech.displayName,
          specialty: tech.specialty,
          avatarUrl: tech.avatarUrl,
          bio: tech.bio,
          experienceYears: tech.experienceYears,
          isPublic: tech.isPublic !== false,
          isAcceptingRequests: tech.isAcceptingRequests !== false,
          displayOrder: tech.displayOrder || 0,
        }),
      })
      if (res.ok) {
        setTechMsg(`✓ Đã lưu hồ sơ kỹ thuật viên: ${tech.displayName}`)
      } else {
        setTechMsg('Không thể cập nhật hồ sơ.')
      }
    } catch {
      setTechMsg('Lỗi kết nối máy chủ.')
    } finally {
      setSavingTechId(null)
    }
  }

  // Reception appointments with Delivery Method (Phase 6C)
  const [receptionAppointments] = useState([
    {
      appointmentId: 501,
      customerName: 'Hoàng Quốc Việt',
      customerPhone: '0933112233',
      modelName: 'Lenovo Legion 5 Pro',
      deliveryMethod: 'SHIP_IN',
      pickupAddress: 'Số 18 Ngõ 86 Cầu Giấy, P. Quan Hoa, Cầu Giấy, Hà Nội',
      issueDescription: '[GỬI CHUYỂN PHÁT GHN] Máy quạt kêu to, bốc khói nhẹ',
      preferredTechnician: 'Lê Hoàng Nam',
    },
    {
      appointmentId: 502,
      customerName: 'Nguyễn Thị Bích',
      customerPhone: '0977889900',
      modelName: 'MacBook Air M1',
      deliveryMethod: 'PICKUP',
      pickupAddress: 'Tòa nhà Landmark 81, P. 22, Bình Thạnh, TP. Hồ Chí Minh',
      issueDescription: '[GIAO NHẬN TẬN NƠI] Màn hình nhấp nháy liên tục',
      preferredTechnician: 'TechCare sắp xếp',
    },
  ])

  const [receptionWaybills] = useState([
    { code: 'GHN-TC-2026110301', customer: 'Nguyễn Văn An', phone: '0912345678', model: 'Dell XPS 15 9520', status: 'IN_TRANSIT', fee: 38000, date: '03/11/2026' },
    { code: 'GHN-TC-2026110294', customer: 'Trần Thị Mai', phone: '0987654321', model: 'MacBook Pro M2 14"', status: 'DELIVERED', fee: 42000, date: '02/11/2026' },
    { code: 'GHN-TC-2026110188', customer: 'Lê Hoàng Nam', phone: '0905123456', model: 'ThinkPad T14 Gen 4', status: 'PICKED', fee: 35000, date: '01/11/2026' },
  ])

  // Mock queue for technicians
  const [techTickets, setTechTickets] = useState([
    { id: 101, code: 'TC-REP-0881', customer: 'Hoàng Minh Tuấn', model: 'Asus ROG Zephyrus G14', issue: 'Nóng máy, quạt rít, sập nguồn', status: 'DIAGNOSING', sn: 'J8N0CV02E19832B', ssdSn: 'S676NF0T102938M' },
    { id: 102, code: 'TC-REP-0882', customer: 'Vũ Hải Đăng', model: 'Dell Latitude 7420', issue: 'Sọc màn hình, liệt phím W, S', status: 'REPAIRING', sn: '8BG7TY2', ssdSn: 'NVME-512-449102' },
  ])

  // Inventory mock
  const [inventoryList] = useState([
    { id: 1, sku: 'PANEL-156-FHD-144', name: 'Màn hình 15.6" FHD 144Hz IPS 40pin', stock: 12, minStock: 5, price: 1450000 },
    { id: 2, sku: 'NVME-1TB-SAMS-980', name: 'SSD Samsung 980 1TB PCIe NVMe M.2', stock: 24, minStock: 8, price: 1850000 },
    { id: 3, sku: 'BAT-DELL-56WH-3CELL', name: 'Pin Dell 56Wh Type 89WH (Zin)', stock: 3, minStock: 5, price: 950000 },
  ])

  // Guard check
  const hasAccessToPath = (path: string): boolean => {
    if (path.startsWith('/admin')) return isAdmin
    if (path.startsWith('/le-tan')) return isReception
    if (path.startsWith('/ky-thuat')) return isTech
    if (path.startsWith('/kho')) return isInventory
    if (path.startsWith('/thu-ngan')) return isCashier
    if (path.startsWith('/ho-tro')) return isSupport
    if (path.startsWith('/tai-khoan')) return true
    return true
  }

  if (!hasAccessToPath(currentPath)) {
    return (
      <div style={{ maxWidth: '800px', margin: '60px auto', padding: '32px', background: '#fff', border: '1px solid #dc2626', borderRadius: '12px', textAlign: 'center' }}>
        <h3 style={{ color: '#dc2626', fontSize: '18px', marginBottom: '8px' }}>Không có quyền truy cập</h3>
        <p style={{ color: '#555', fontSize: '13.5px', marginBottom: '20px' }}>
          Tài khoản <strong>{user.userName}</strong> (Vai trò: {user.roles.join(', ')}) không có quyền truy cập vào phân hệ này.
        </p>
        <button type="button" className="button" onClick={onNavigateHome}>
          Quay về trang chủ
        </button>
      </div>
    )
  }

  return (
    <div style={{ minHeight: '100vh', background: '#f7f7f5', display: 'flex', flexDirection: 'column' }}>
      {/* Top Role Portal Navigation Bar */}
      <header
        style={{
          background: '#111111',
          color: '#ffffff',
          padding: '12px 32px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '12px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          <button
            type="button"
            onClick={onNavigateHome}
            style={{ background: 'transparent', border: 'none', color: '#fff', fontWeight: 800, fontSize: '16px', cursor: 'pointer', letterSpacing: '-0.02em' }}
          >
            TECHCARE / PORTAL
          </button>

          {/* Navigation Links based on User Roles */}
          <nav style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
            {isAdmin && (
              <button
                type="button"
                onClick={() => setCurrentPath('/admin')}
                style={{
                  background: currentPath.startsWith('/admin') ? '#333' : 'transparent',
                  color: '#fff',
                  border: 'none',
                  padding: '6px 12px',
                  borderRadius: '6px',
                  fontSize: '12px',
                  cursor: 'pointer',
                  fontWeight: 600,
                }}
              >
                Quản trị Hệ thống
              </button>
            )}
            {isReception && (
              <button
                type="button"
                onClick={() => setCurrentPath('/le-tan')}
                style={{
                  background: currentPath.startsWith('/le-tan') ? '#333' : 'transparent',
                  color: '#fff',
                  border: 'none',
                  padding: '6px 12px',
                  borderRadius: '6px',
                  fontSize: '12px',
                  cursor: 'pointer',
                  fontWeight: 600,
                }}
              >
                Lễ tân & Vận đơn
              </button>
            )}
            {isTech && (
              <button
                type="button"
                onClick={() => setCurrentPath('/ky-thuat')}
                style={{
                  background: currentPath.startsWith('/ky-thuat') ? '#333' : 'transparent',
                  color: '#fff',
                  border: 'none',
                  padding: '6px 12px',
                  borderRadius: '6px',
                  fontSize: '12px',
                  cursor: 'pointer',
                  fontWeight: 600,
                }}
              >
                Kỹ thuật viên
              </button>
            )}
            {isInventory && (
              <button
                type="button"
                onClick={() => setCurrentPath('/kho')}
                style={{
                  background: currentPath.startsWith('/kho') ? '#333' : 'transparent',
                  color: '#fff',
                  border: 'none',
                  padding: '6px 12px',
                  borderRadius: '6px',
                  fontSize: '12px',
                  cursor: 'pointer',
                  fontWeight: 600,
                }}
              >
                Kho linh kiện
              </button>
            )}
            {isCashier && (
              <button
                type="button"
                onClick={() => setCurrentPath('/thu-ngan')}
                style={{
                  background: currentPath.startsWith('/thu-ngan') ? '#333' : 'transparent',
                  color: '#fff',
                  border: 'none',
                  padding: '6px 12px',
                  borderRadius: '6px',
                  fontSize: '12px',
                  cursor: 'pointer',
                  fontWeight: 600,
                }}
              >
                Thu ngân
              </button>
            )}
            {isSupport && (
              <button
                type="button"
                onClick={() => setCurrentPath('/ho-tro')}
                style={{
                  background: currentPath.startsWith('/ho-tro') ? '#333' : 'transparent',
                  color: '#fff',
                  border: 'none',
                  padding: '6px 12px',
                  borderRadius: '6px',
                  fontSize: '12px',
                  cursor: 'pointer',
                  fontWeight: 600,
                }}
              >
                Hỗ trợ Khách hàng
              </button>
            )}
            <button
              type="button"
              onClick={() => setCurrentPath('/tai-khoan')}
              style={{
                background: currentPath.startsWith('/tai-khoan') ? '#333' : 'transparent',
                color: '#fff',
                border: 'none',
                padding: '6px 12px',
                borderRadius: '6px',
                fontSize: '12px',
                cursor: 'pointer',
                fontWeight: 600,
              }}
            >
              Tài khoản
            </button>
          </nav>
        </div>

        {/* User Info & Logout */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
          <div style={{ textAlign: 'right' }}>
            <div style={{ fontSize: '12.5px', fontWeight: 600 }}>{user.userName}</div>
            <div style={{ fontSize: '10.5px', color: '#aaa' }}>
              Vai trò: {user.roles.join(', ')} {user.branchId ? `| CN #${user.branchId}` : ''}
            </div>
          </div>
          <button
            type="button"
            onClick={onLogout}
            style={{
              background: '#27272a',
              border: '1px solid #444',
              color: '#ffffff',
              padding: '6px 12px',
              borderRadius: '6px',
              fontSize: '11.5px',
              cursor: 'pointer',
            }}
          >
            Đăng xuất
          </button>
        </div>
      </header>

      {/* Main Content Area */}
      <main style={{ flex: 1 }}>
        {/* 1. ADMIN PORTAL */}
        {currentPath.startsWith('/admin') && (
          <div>
            {/* Sub-menu */}
            <div style={{ background: '#ffffff', borderBottom: '1px solid var(--line)', padding: '8px 32px', display: 'flex', gap: '16px' }}>
              <button
                type="button"
                onClick={() => setActiveAdminSubTab('KNOWLEDGE')}
                style={{
                  background: 'transparent',
                  border: 'none',
                  borderBottom: activeAdminSubTab === 'KNOWLEDGE' ? '2px solid #111' : 'none',
                  fontWeight: activeAdminSubTab === 'KNOWLEDGE' ? 700 : 500,
                  padding: '8px 4px',
                  fontSize: '13px',
                  cursor: 'pointer',
                }}
              >
                Tri thức AI (RAG)
              </button>
              <button
                type="button"
                onClick={() => setActiveAdminSubTab('USERS')}
                style={{
                  background: 'transparent',
                  border: 'none',
                  borderBottom: activeAdminSubTab === 'USERS' ? '2px solid #111' : 'none',
                  fontWeight: activeAdminSubTab === 'USERS' ? 700 : 500,
                  padding: '8px 4px',
                  fontSize: '13px',
                  cursor: 'pointer',
                }}
              >
                Nhân sự & Phân quyền (RBAC)
              </button>
              <button
                type="button"
                onClick={() => setActiveAdminSubTab('AUDIT')}
                style={{
                  background: 'transparent',
                  border: 'none',
                  borderBottom: activeAdminSubTab === 'AUDIT' ? '2px solid #111' : 'none',
                  fontWeight: activeAdminSubTab === 'AUDIT' ? 700 : 500,
                  padding: '8px 4px',
                  fontSize: '13px',
                  cursor: 'pointer',
                }}
              >
                Nhật ký Kiểm toán (Audit Logs)
              </button>
              <button
                type="button"
                onClick={() => setActiveAdminSubTab('INTEGRATIONS')}
                style={{
                  background: 'transparent',
                  border: 'none',
                  borderBottom: activeAdminSubTab === 'INTEGRATIONS' ? '2px solid #111' : 'none',
                  fontWeight: activeAdminSubTab === 'INTEGRATIONS' ? 700 : 500,
                  padding: '8px 4px',
                  fontSize: '13px',
                  cursor: 'pointer',
                }}
              >
                Cấu hình Tích hợp (Integrations)
              </button>
            </div>

            {activeAdminSubTab === 'KNOWLEDGE' && <KnowledgeAdmin />}

            {activeAdminSubTab === 'USERS' && (
              <div style={{ maxWidth: '1200px', margin: '24px auto', padding: '0 24px' }}>
                <h3 style={{ fontSize: '18px', fontWeight: 600, marginBottom: '12px' }}>Danh sách tài khoản & Vai trò</h3>
                <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '20px' }}>
                  <p style={{ fontSize: '13px', color: '#666', lineHeight: 1.6 }}>
                    Hệ thống xác thực JWT Bearer phân quyền theo từng Permission. Các vai trò gồm: <strong>ADMIN</strong>, <strong>RECEPTIONIST</strong>, <strong>TECHNICIAN</strong>, <strong>INVENTORY</strong>, <strong>CASHIER</strong>, <strong>SUPPORT</strong>.
                  </p>
                </div>

                {/* Quản lý hồ sơ Kỹ thuật viên (Phase 6B) */}
                <div style={{ marginTop: '24px' }}>
                  <h4 style={{ fontSize: '16px', fontWeight: 600, marginBottom: '8px' }}>
                    Hồ sơ Kỹ thuật viên (Khách chọn & Đánh giá sao)
                  </h4>
                  {techMsg && (
                    <div style={{ padding: '8px 12px', background: '#ecfdf5', color: '#065f46', borderRadius: '6px', fontSize: '12px', marginBottom: '12px' }}>
                      {techMsg}
                    </div>
                  )}
                  {adminTechs.length === 0 ? (
                    <div style={{ padding: '14px', background: '#fafaf8', border: '1px solid var(--line)', borderRadius: '8px', fontSize: '12.5px', color: '#666' }}>
                      Chưa có kỹ thuật viên nào trong hệ thống hoặc đang tải danh sách...
                    </div>
                  ) : (
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(320px, 1fr))', gap: '14px' }}>
                      {adminTechs.map((tech) => (
                        <div key={tech.userId} style={{ border: '1px solid var(--line)', borderRadius: '10px', padding: '16px', background: '#fff' }}>
                          <div style={{ display: 'flex', gap: '12px', alignItems: 'center', marginBottom: '12px' }}>
                            <img
                              src={tech.avatarUrl || 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&q=80&w=120'}
                              alt={tech.displayName}
                              style={{ width: '42px', height: '42px', borderRadius: '50%', objectFit: 'cover' }}
                            />
                            <div>
                              <div style={{ fontWeight: 600, fontSize: '14px' }}>{tech.displayName}</div>
                              <div style={{ fontSize: '11.5px', color: 'var(--sub)' }}>
                                {tech.ratingAverage !== null && tech.ratingCount > 0
                                  ? `★ ${Number(tech.ratingAverage).toFixed(1)} (${tech.ratingCount} lượt)`
                                  : 'Chưa có đánh giá'} · {tech.completedTicketsCount} đơn xong
                              </div>
                            </div>
                          </div>

                          <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', fontSize: '12px' }}>
                            <div>
                              <label style={{ color: 'var(--sub)', display: 'block', marginBottom: '2px' }}>Tên hiển thị:</label>
                              <input
                                type="text"
                                value={tech.displayName}
                                onChange={(e) => {
                                  const val = e.target.value
                                  setAdminTechs((prev) => prev.map((t) => (t.userId === tech.userId ? { ...t, displayName: val } : t)))
                                }}
                                style={{ width: '100%', padding: '6px 8px', border: '1px solid var(--line)', borderRadius: '6px' }}
                              />
                            </div>
                            <div>
                              <label style={{ color: 'var(--sub)', display: 'block', marginBottom: '2px' }}>Chuyên môn:</label>
                              <input
                                type="text"
                                value={tech.specialty}
                                onChange={(e) => {
                                  const val = e.target.value
                                  setAdminTechs((prev) => prev.map((t) => (t.userId === tech.userId ? { ...t, specialty: val } : t)))
                                }}
                                style={{ width: '100%', padding: '6px 8px', border: '1px solid var(--line)', borderRadius: '6px' }}
                              />
                            </div>
                            <div style={{ display: 'flex', gap: '16px', marginTop: '4px' }}>
                              <label style={{ display: 'flex', alignItems: 'center', gap: '6px', cursor: 'pointer' }}>
                                <input
                                  type="checkbox"
                                  checked={tech.isPublic !== false}
                                  onChange={(e) => {
                                    const val = e.target.checked
                                    setAdminTechs((prev) => prev.map((t) => (t.userId === tech.userId ? { ...t, isPublic: val } : t)))
                                  }}
                                />
                                Công khai cho khách chọn
                              </label>
                              <label style={{ display: 'flex', alignItems: 'center', gap: '6px', cursor: 'pointer' }}>
                                <input
                                  type="checkbox"
                                  checked={tech.isAcceptingRequests !== false}
                                  onChange={(e) => {
                                    const val = e.target.checked
                                    setAdminTechs((prev) => prev.map((t) => (t.userId === tech.userId ? { ...t, isAcceptingRequests: val } : t)))
                                  }}
                                />
                                Đang nhận yêu cầu
                              </label>
                            </div>
                          </div>

                          <div style={{ marginTop: '12px', textAlign: 'right' }}>
                            <button
                              type="button"
                              onClick={() => handleUpdateTechProfile(tech)}
                              disabled={savingTechId === tech.userId}
                              style={{ background: '#111', color: '#fff', border: 'none', padding: '6px 14px', borderRadius: '6px', fontSize: '11.5px', cursor: 'pointer' }}
                            >
                              {savingTechId === tech.userId ? 'Đang lưu...' : 'Lưu hồ sơ'}
                            </button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            )}

            {activeAdminSubTab === 'AUDIT' && (
              <div style={{ maxWidth: '1200px', margin: '24px auto', padding: '0 24px' }}>
                <h3 style={{ fontSize: '18px', fontWeight: 600, marginBottom: '12px' }}>Nhật ký kiểm toán hệ thống</h3>
                <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '20px' }}>
                  <p style={{ fontSize: '13px', color: '#666' }}>
                    Tất cả hành động sửa chữa, thay đổi trạng thái và xuất linh kiện được ghi log bất biến vào <code>Security.AuditLogs</code>.
                  </p>
                </div>
              </div>
            )}

            {activeAdminSubTab === 'INTEGRATIONS' && (
              <div style={{ maxWidth: '1200px', margin: '24px auto', padding: '0 24px' }}>
                <div style={{ marginBottom: '20px' }}>
                  <div className="eyebrow">CẤU HÌNH TÍCH HỢP HỆ THỐNG</div>
                  <h3 style={{ fontSize: '20px', fontWeight: 600 }}>Dịch vụ bên thứ ba & Thông báo</h3>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(360px, 1fr))', gap: '20px' }}>
                  {/* Card 1: Email SMTP */}
                  <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '20px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
                      <h4 style={{ fontSize: '15px', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span>✉️</span> Email (SMTP)
                      </h4>
                      <span
                        style={{
                          fontSize: '11.5px',
                          fontWeight: 600,
                          padding: '3px 10px',
                          borderRadius: '999px',
                          background: integrations?.smtpConfigured ? '#ecfdf5' : '#fef9c3',
                          color: integrations?.smtpConfigured ? '#065f46' : '#854d0e',
                          border: `1px solid ${integrations?.smtpConfigured ? '#a7f3d0' : '#fef08a'}`,
                        }}
                      >
                        {integrations?.smtpConfigured ? '✓ Đã cấu hình' : 'Dev: Chế độ LogOnly'}
                      </span>
                    </div>
                    <p style={{ fontSize: '12.5px', color: '#666', lineHeight: 1.6, marginBottom: '14px' }}>
                      {integrations?.smtpConfigured
                        ? 'Máy chủ SMTP đã được kích hoạt. Mã xác thực đăng ký, quên mật khẩu và thông báo sửa máy được gửi thật tới hòm thư khách hàng.'
                        : 'Chưa cung cấp cấu hình máy chủ SMTP. Hệ thống đang hoạt động ở chế độ LogOnly (ghi log máy chủ để phát triển & kiểm thử miễn phí, không bị gián đoạn).'}
                    </p>
                    <div style={{ background: '#fafaf8', padding: '12px', borderRadius: '8px', fontSize: '11.5px', color: '#555', fontFamily: 'monospace' }}>
                      Biến môi trường hỗ trợ:
                      <br />• TECHCARE_SMTP_HOST
                      <br />• TECHCARE_SMTP_PORT (587 / 465)
                      <br />• TECHCARE_SMTP_USER
                      <br />• TECHCARE_SMTP_PASSWORD
                      <br />• TECHCARE_SMTP_FROM
                    </div>
                    <div style={{ fontSize: '11.5px', color: 'var(--sub)', marginTop: '10px' }}>
                      * Nicole có thể điền cấu hình SMTP vào biến môi trường hoặc user-secrets bất cứ lúc nào mà không cần sửa code.
                    </div>
                  </div>

                  {/* Card 2: GHN Express */}
                  <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '20px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
                      <h4 style={{ fontSize: '15px', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span>🚚</span> Giao Hàng Nhanh (GHN Express)
                      </h4>
                      <span
                        style={{
                          fontSize: '11.5px',
                          fontWeight: 600,
                          padding: '3px 10px',
                          borderRadius: '999px',
                          background: '#ecfdf5',
                          color: '#065f46',
                          border: '1px solid #a7f3d0',
                        }}
                      >
                        ✓ Sẵn sàng Webhook & API
                      </span>
                    </div>
                    <p style={{ fontSize: '12.5px', color: '#666', lineHeight: 1.6, marginBottom: '14px' }}>
                      Tích hợp tính cước tự động, sinh mã vận đơn, bản đồ lộ trình Leaflet và webhook cập nhật trạng thái đơn bưu phẩm máy tính xách tay.
                    </p>
                    <div style={{ background: '#fafaf8', padding: '12px', borderRadius: '8px', fontSize: '11.5px', color: '#555', fontFamily: 'monospace' }}>
                      Webhook endpoint:
                      <br />POST /api/v1/shipping/webhooks/ghn
                    </div>
                  </div>
                </div>
              </div>
            )}
          </div>
        )}

        {/* 2. RECEPTIONIST PORTAL & WAYBILL MANAGEMENT (/le-tan & /le-tan/van-don) */}
        {currentPath.startsWith('/le-tan') && (
          <div style={{ maxWidth: '1200px', margin: '24px auto', padding: '0 24px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
              <div>
                <div className="eyebrow">PHÂN HỆ LỄ TÂN & ĐIỀU PHỐI</div>
                <h2 style={{ fontSize: '24px', fontWeight: 600 }}>Quản lý Tiếp nhận & Vận đơn GHN</h2>
              </div>
              <button
                type="button"
                className="button"
                onClick={() => alert('Chức năng tạo đơn GHN mới đã sẵn sàng tích hợp với GHN Staging.')}
              >
                + Lập vận đơn GHN mới
              </button>
            </div>

            {/* INCOMING APPOINTMENTS WITH DELIVERY METHOD */}
            <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', overflow: 'hidden', marginBottom: '24px' }}>
              <div style={{ padding: '14px 20px', borderBottom: '1px solid var(--line)', background: '#fafaf8', fontWeight: 600, fontSize: '13px' }}>
                Lịch hẹn đặt máy & Tiếp nhận (Hình thức nhận máy)
              </div>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--line)', color: '#666' }}>
                    <th style={{ padding: '12px 16px' }}>Mã hẹn</th>
                    <th style={{ padding: '12px 16px' }}>Khách hàng & SĐT</th>
                    <th style={{ padding: '12px 16px' }}>Thiết bị</th>
                    <th style={{ padding: '12px 16px' }}>Hình thức gửi máy</th>
                    <th style={{ padding: '12px 16px' }}>Địa chỉ nhận / giao</th>
                    <th style={{ padding: '12px 16px' }}>Kỹ thuật viên</th>
                    <th style={{ padding: '12px 16px', textAlign: 'right' }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {receptionAppointments.map((apt) => (
                    <tr key={apt.appointmentId} style={{ borderBottom: '1px solid #f0f0ee' }}>
                      <td style={{ padding: '12px 16px', fontFamily: 'monospace', fontWeight: 700 }}>#{apt.appointmentId}</td>
                      <td style={{ padding: '12px 16px' }}>
                        <div>{apt.customerName}</div>
                        <span style={{ fontSize: '11px', color: '#777' }}>{apt.customerPhone}</span>
                      </td>
                      <td style={{ padding: '12px 16px' }}>{apt.modelName}</td>
                      <td style={{ padding: '12px 16px' }}>
                        <span
                          style={{
                            background: apt.deliveryMethod === 'SHIP_IN' ? '#eff6ff' : apt.deliveryMethod === 'PICKUP' ? '#fef3c7' : '#f4f4f2',
                            color: apt.deliveryMethod === 'SHIP_IN' ? '#1d4ed8' : apt.deliveryMethod === 'PICKUP' ? '#92400e' : '#333',
                            padding: '3px 8px',
                            borderRadius: '4px',
                            fontSize: '11px',
                            fontWeight: 600,
                          }}
                        >
                          {apt.deliveryMethod === 'SHIP_IN' ? 'Gửi ship bưu điện' : apt.deliveryMethod === 'PICKUP' ? 'TechCare nhận tận nơi' : 'Tại quầy'}
                        </span>
                      </td>
                      <td style={{ padding: '12px 16px', maxWidth: '240px', fontSize: '12px', color: '#555' }}>
                        {apt.pickupAddress || 'Tại trung tâm TechCare'}
                      </td>
                      <td style={{ padding: '12px 16px', fontSize: '12px' }}>{apt.preferredTechnician}</td>
                      <td style={{ padding: '12px 16px', textAlign: 'right' }}>
                        {(apt.deliveryMethod === 'SHIP_IN' || apt.deliveryMethod === 'PICKUP') ? (
                          <button
                            type="button"
                            onClick={() => alert(`Khởi tạo vận đơn GHN Express cho lịch hẹn #${apt.appointmentId}:\n- Khách hàng: ${apt.customerName} (${apt.customerPhone})\n- Địa chỉ lấy/giao máy: ${apt.pickupAddress}\n- Thiết bị: ${apt.modelName}\n\nĐã tự động nạp địa chỉ khách vào hệ thống GHN!`)}
                            style={{ background: '#111', color: '#fff', border: 'none', padding: '5px 10px', borderRadius: '4px', fontSize: '11px', cursor: 'pointer', fontWeight: 600 }}
                          >
                            + Tạo vận đơn
                          </button>
                        ) : (
                          <span style={{ color: '#888', fontSize: '11px' }}>Khách đến quầy</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', overflow: 'hidden' }}>
              <div style={{ padding: '14px 20px', borderBottom: '1px solid var(--line)', background: '#fafaf8', fontWeight: 600, fontSize: '13px' }}>
                Danh sách Vận đơn Chuyển phát Laptop (GHN Express)
              </div>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--line)', color: '#666' }}>
                    <th style={{ padding: '12px 16px' }}>Mã vận đơn GHN</th>
                    <th style={{ padding: '12px 16px' }}>Khách hàng & SĐT</th>
                    <th style={{ padding: '12px 16px' }}>Thiết bị</th>
                    <th style={{ padding: '12px 16px' }}>Cước phí</th>
                    <th style={{ padding: '12px 16px' }}>Trạng thái giao nhận</th>
                    <th style={{ padding: '12px 16px' }}>Ngày tạo</th>
                    <th style={{ padding: '12px 16px', textAlign: 'right' }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {receptionWaybills.map((w) => (
                    <tr key={w.code} style={{ borderBottom: '1px solid #f0f0ee' }}>
                      <td style={{ padding: '12px 16px', fontFamily: 'monospace', fontWeight: 700 }}>{w.code}</td>
                      <td style={{ padding: '12px 16px' }}>
                        <div>{w.customer}</div>
                        <span style={{ fontSize: '11px', color: '#777' }}>{w.phone}</span>
                      </td>
                      <td style={{ padding: '12px 16px' }}>{w.model}</td>
                      <td style={{ padding: '12px 16px' }}>{w.fee.toLocaleString('vi-VN')}đ</td>
                      <td style={{ padding: '12px 16px' }}>
                        <span style={{ background: '#111', color: '#fff', padding: '2px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>
                          {w.status}
                        </span>
                      </td>
                      <td style={{ padding: '12px 16px', color: '#777' }}>{w.date}</td>
                      <td style={{ padding: '12px 16px', textAlign: 'right' }}>
                        <button
                          type="button"
                          onClick={() => alert(`In tem phiếu bưu phẩm GHN cho mã: ${w.code}`)}
                          style={{ background: 'transparent', border: '1px solid #111', padding: '3px 8px', borderRadius: '4px', fontSize: '11px', cursor: 'pointer' }}
                        >
                          In tem GHN
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* 3. TECHNICIAN PORTAL (/ky-thuat) */}
        {currentPath.startsWith('/ky-thuat') && (
          <div style={{ maxWidth: '1200px', margin: '24px auto', padding: '0 24px' }}>
            <div className="eyebrow">XƯỞNG KỸ THUẬT TIẾP NHẬN</div>
            <h2 style={{ fontSize: '24px', fontWeight: 600, marginBottom: '20px' }}>Hàng chờ Sửa chữa & Đối chiếu Serial Chống tráo</h2>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              {techTickets.map((t) => (
                <div key={t.id} style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '20px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                    <h3 style={{ fontSize: '16px', fontWeight: 700 }}>
                      {t.code} — {t.model} ({t.customer})
                    </h3>
                    <span style={{ background: '#111', color: '#fff', padding: '3px 10px', borderRadius: '4px', fontSize: '11px', fontWeight: 600 }}>
                      {t.status}
                    </span>
                  </div>

                  <div style={{ fontSize: '13px', color: '#555', marginBottom: '12px' }}>
                    <strong>Hiện trạng lỗi:</strong> {t.issue}
                  </div>

                  {/* Serial Anti-tamper comparison block */}
                  <div style={{ background: '#fbfbf9', border: '1px solid #e4e4e0', borderRadius: '8px', padding: '12px 16px', marginBottom: '14px', fontSize: '12.5px' }}>
                    <div style={{ fontWeight: 600, color: '#111', marginBottom: '4px' }}>
                      Bằng chứng đối chiếu linh kiện trước & sau sửa (Chống tráo đổi):
                    </div>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                      <div>Serial máy (BIOS): <code style={{ background: '#eee', padding: '2px 6px', borderRadius: '3px', fontWeight: 700 }}>{t.sn}</code></div>
                      <div>Serial SSD (Ổ cứng): <code style={{ background: '#eee', padding: '2px 6px', borderRadius: '3px', fontWeight: 700 }}>{t.ssdSn}</code></div>
                    </div>
                  </div>

                  <div style={{ display: 'flex', gap: '8px' }}>
                    <button
                      type="button"
                      className="button"
                      style={{ padding: '7px 16px', fontSize: '12px' }}
                      onClick={() => {
                        setTechTickets((prev) =>
                          prev.map((item) => (item.id === t.id ? { ...item, status: 'READY' } : item))
                        )
                        alert(`Đã cập nhật trạng thái phiếu ${t.code} thành READY (Sẵn sàng bàn giao).`)
                      }}
                    >
                      Hoàn thành & Bàn giao (READY)
                    </button>
                    <button
                      type="button"
                      style={{ background: '#fff', border: '1px solid #111', padding: '7px 14px', borderRadius: '999px', fontSize: '12px', cursor: 'pointer' }}
                      onClick={() => alert(`Yêu cầu xuất kho linh kiện cho ${t.code}`)}
                    >
                      Yêu cầu xuất linh kiện
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* 4. INVENTORY PORTAL (/kho) */}
        {currentPath.startsWith('/kho') && (
          <div style={{ maxWidth: '1200px', margin: '24px auto', padding: '0 24px' }}>
            <div className="eyebrow">QUẢN LÝ KHO LINH KIỆN</div>
            <h2 style={{ fontSize: '24px', fontWeight: 600, marginBottom: '20px' }}>Tồn kho & Cảnh báo Linh kiện</h2>

            <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', overflow: 'hidden' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
                <thead>
                  <tr style={{ background: '#fafaf8', borderBottom: '1px solid var(--line)', color: '#666' }}>
                    <th style={{ padding: '12px 16px' }}>SKU</th>
                    <th style={{ padding: '12px 16px' }}>Tên linh kiện</th>
                    <th style={{ padding: '12px 16px' }}>Số lượng tồn</th>
                    <th style={{ padding: '12px 16px' }}>Tồn tối thiểu</th>
                    <th style={{ padding: '12px 16px' }}>Đơn giá niêm yết</th>
                    <th style={{ padding: '12px 16px' }}>Cảnh báo</th>
                  </tr>
                </thead>
                <tbody>
                  {inventoryList.map((item) => (
                    <tr key={item.id} style={{ borderBottom: '1px solid #f0f0ee' }}>
                      <td style={{ padding: '12px 16px', fontFamily: 'monospace', fontWeight: 600 }}>{item.sku}</td>
                      <td style={{ padding: '12px 16px', fontWeight: 500 }}>{item.name}</td>
                      <td style={{ padding: '12px 16px', fontWeight: 700 }}>{item.stock} cái</td>
                      <td style={{ padding: '12px 16px', color: '#777' }}>{item.minStock} cái</td>
                      <td style={{ padding: '12px 16px' }}>{item.price.toLocaleString('vi-VN')}đ</td>
                      <td style={{ padding: '12px 16px' }}>
                        {item.stock <= item.minStock ? (
                          <span style={{ background: '#fef2f2', color: '#dc2626', border: '1px solid #fca5a5', padding: '2px 8px', borderRadius: '4px', fontSize: '10.5px', fontWeight: 700 }}>
                            CẦN NHẬP THÊM
                          </span>
                        ) : (
                          <span style={{ color: '#059669', fontSize: '11px', fontWeight: 600 }}>Ổn định</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* 5. CASHIER PORTAL (/thu-ngan) */}
        {currentPath.startsWith('/thu-ngan') && (
          <div style={{ maxWidth: '1200px', margin: '24px auto', padding: '0 24px' }}>
            <div className="eyebrow">QUẦY THU NGÂN</div>
            <h2 style={{ fontSize: '24px', fontWeight: 600, marginBottom: '20px' }}>Thanh toán Phiếu & Xuất Hóa đơn Điện tử</h2>

            <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '12px', padding: '24px' }}>
              <p style={{ fontSize: '13.5px', color: '#555', lineHeight: 1.6 }}>
                Hỗ trợ thu tiền mặt, chuyển khoản qua mã VietQR động, quẹt thẻ POS hoặc cổng VNPAY. Xuất hóa đơn GTGT điện tử ngay khi hoàn tất bàn giao.
              </p>
            </div>
          </div>
        )}

        {/* 6. SUPPORT LIVE CHAT (/ho-tro) */}
        {currentPath.startsWith('/ho-tro') && <SupportChatInbox />}

        {/* 7. CUSTOMER ACCOUNT (/tai-khoan) */}
        {currentPath.startsWith('/tai-khoan') && <CustomerPortal user={user} onLogout={onLogout} />}
      </main>
    </div>
  )
}
