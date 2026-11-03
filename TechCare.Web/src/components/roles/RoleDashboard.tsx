import { useState } from 'react'
import { KnowledgeAdmin } from '../admin/KnowledgeAdmin'
import { SupportChatInbox } from '../support/SupportChatInbox'
import { CustomerPortal } from '../customer/CustomerPortal'

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

  const [activeAdminSubTab, setActiveAdminSubTab] = useState<'KNOWLEDGE' | 'USERS' | 'AUDIT'>('KNOWLEDGE')
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
