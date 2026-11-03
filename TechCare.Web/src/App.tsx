import React, { useState, useEffect } from 'react'
import { SYSTEM_NOTIFICATIONS } from './data/notifications'
import { KindredCanvas } from './components/KindredCanvas'
import { BookingSection } from './components/BookingSection'
import { TrackingSection } from './components/TrackingSection'
import { ChatWidget } from './components/ChatWidget'
import { RoleDashboard, type AuthUser } from './components/roles/RoleDashboard'
import { API_BASE_URL } from './api/client'
import './App.css'

type TabType = 'home' | 'services' | 'pricing' | 'booking' | 'tracking'

export default function App() {
  const [activeTab, setActiveTab] = useState<TabType>('home')
  const [selectedIssue, setSelectedIssue] = useState('')
  const [isLoginOpen, setIsLoginOpen] = useState(false)
  const [isNotifOpen, setIsNotifOpen] = useState(false)
  const [pricingBrand, setPricingBrand] = useState('ALL')
  const [trackingQuery, setTrackingQuery] = useState<{ code: string; phone: string } | null>(null)
  
  // Role Authentication & Routing State
  const [currentUser, setCurrentUser] = useState<AuthUser | null>(() => {
    try {
      const saved = localStorage.getItem('tc_user')
      return saved ? JSON.parse(saved) : null
    } catch {
      return null
    }
  })
  const [currentRolePath, setCurrentRolePath] = useState<string | null>(null)

  // Unified Auth State (Đợt 6: Email Registration & Verification)
  type AuthModalTab = 'LOGIN' | 'REGISTER' | 'VERIFY_EMAIL' | 'FORGOT_PASSWORD' | 'RESET_PASSWORD'
  const [authTab, setAuthTab] = useState<AuthModalTab>('LOGIN')
  const [authError, setAuthError] = useState<string | null>(null)
  const [authMessage, setAuthMessage] = useState<string | null>(null)
  const [authLoading, setAuthLoading] = useState(false)

  // Login inputs
  const [loginIdentifier, setLoginIdentifier] = useState('admin')
  const [loginPassword, setLoginPassword] = useState('Admin@123')

  // Register inputs
  const [regFullName, setRegFullName] = useState('')
  const [regEmail, setRegEmail] = useState('')
  const [regPhone, setRegPhone] = useState('')
  const [regPassword, setRegPassword] = useState('')
  const [regConfirmPassword, setRegConfirmPassword] = useState('')

  // Verify email & forgot password inputs
  const [verifyEmail, setVerifyEmail] = useState('')
  const [verifyCode, setVerifyCode] = useState('')
  const [resendCountdown, setResendCountdown] = useState(0)
  const [forgotEmail, setForgotEmail] = useState('')
  const [resetEmail, setResetEmail] = useState('')
  const [resetCode, setResetCode] = useState('')
  const [resetNewPassword, setResetNewPassword] = useState('')
  const [resetConfirmPassword, setResetConfirmPassword] = useState('')

  useEffect(() => {
    if (resendCountdown <= 0) return
    const timer = setInterval(() => {
      setResendCountdown((prev) => prev - 1)
    }, 1000)
    return () => clearInterval(timer)
  }, [resendCountdown])

  const handleRegisterSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setAuthError(null)
    setAuthMessage(null)

    const phoneRegex = /^0(3|5|7|8|9)\d{8}$/
    if (!phoneRegex.test(regPhone.trim())) {
      setAuthError('Số điện thoại không hợp lệ. Vui lòng nhập số di động Việt Nam 10 chữ số (VD: 0912345678).')
      return
    }
    if (!regEmail.includes('@') || !regEmail.includes('.')) {
      setAuthError('Email không hợp lệ.')
      return
    }
    if (regPassword.length < 8) {
      setAuthError('Mật khẩu phải có tối thiểu 8 ký tự.')
      return
    }
    if (!/[A-Za-z]/.test(regPassword) || !/\d/.test(regPassword)) {
      setAuthError('Mật khẩu phải chứa cả chữ cái và số.')
      return
    }
    if (regPassword !== regConfirmPassword) {
      setAuthError('Mật khẩu xác nhận không khớp.')
      return
    }

    setAuthLoading(true)
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          fullName: regFullName.trim(),
          email: regEmail.trim(),
          phoneNumber: regPhone.trim(),
          password: regPassword,
          confirmPassword: regConfirmPassword,
        }),
      })

      const data = await res.json().catch(() => null)
      if (res.ok) {
        setVerifyEmail(regEmail.trim())
        setAuthTab('VERIFY_EMAIL')
        setAuthMessage(data?.message || 'Đăng ký thành công! Vui lòng kiểm tra email để lấy mã xác thực 6 số (môi trường dev: xem trong log server).')
        setResendCountdown(60)
      } else {
        setAuthError(data?.detail || data?.message || 'Đăng ký tài khoản không thành công. Vui lòng kiểm tra lại thông tin.')
      }
    } catch {
      setAuthError('Không thể kết nối đến máy chủ. Vui lòng thử lại sau.')
    } finally {
      setAuthLoading(false)
    }
  }

  const handleVerifyEmailSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!verifyCode.trim() || verifyCode.trim().length !== 6) {
      setAuthError('Vui lòng nhập đầy đủ mã xác thực 6 chữ số.')
      return
    }
    setAuthLoading(true)
    setAuthError(null)
    setAuthMessage(null)

    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/auth/verify-email`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          email: verifyEmail.trim(),
          code: verifyCode.trim(),
        }),
      })

      const data = await res.json().catch(() => null)
      if (res.ok) {
        const authUser: AuthUser = {
          userId: data.user?.userId || data.userId || 0,
          userName: data.user?.userName || verifyEmail.trim(),
          email: data.user?.email || verifyEmail.trim(),
          roles: data.user?.roles || ['CUSTOMER'],
          permissions: data.user?.permissions || [],
          token: data.accessToken,
        }
        localStorage.setItem('tc_token', data.accessToken)
        localStorage.setItem('tc_user', JSON.stringify(authUser))
        setCurrentUser(authUser)
        setIsLoginOpen(false)
        setCurrentRolePath('/tai-khoan')
        setAuthTab('LOGIN')
        setVerifyCode('')
      } else {
        setAuthError(data?.detail || data?.message || 'Mã xác thực không đúng hoặc đã hết hạn.')
      }
    } catch {
      setAuthError('Lỗi kết nối máy chủ khi xác thực.')
    } finally {
      setAuthLoading(false)
    }
  }

  const handleResendEmailCode = async () => {
    if (resendCountdown > 0) return
    setAuthLoading(true)
    setAuthError(null)
    setAuthMessage(null)
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/auth/resend-email-code`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: verifyEmail.trim() }),
      })
      const data = await res.json().catch(() => null)
      if (res.ok) {
        setAuthMessage(data?.message || 'Mã xác thực mới đã được gửi tới email của bạn.')
        setResendCountdown(60)
      } else {
        setAuthError(data?.detail || data?.message || 'Không thể gửi lại mã. Vui lòng thử lại sau.')
      }
    } catch {
      setAuthError('Không thể kết nối đến máy chủ.')
    } finally {
      setAuthLoading(false)
    }
  }

  const handleForgotPasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!forgotEmail.trim()) {
      setAuthError('Vui lòng nhập địa chỉ email.')
      return
    }
    setAuthLoading(true)
    setAuthError(null)
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/auth/forgot-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: forgotEmail.trim() }),
      })
      const data = await res.json().catch(() => null)
      setResetEmail(forgotEmail.trim())
      setAuthTab('RESET_PASSWORD')
      setAuthMessage(data?.message || 'Nếu email tồn tại trong hệ thống, mã đặt lại mật khẩu đã được gửi đến hộp thư.')
      setResendCountdown(60)
    } catch {
      setAuthError('Không thể kết nối đến máy chủ.')
    } finally {
      setAuthLoading(false)
    }
  }

  const handleResetPasswordSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!resetCode.trim() || resetCode.trim().length !== 6) {
      setAuthError('Vui lòng nhập đúng mã xác thực 6 số.')
      return
    }
    if (resetNewPassword.length < 8 || !/[A-Za-z]/.test(resetNewPassword) || !/\d/.test(resetNewPassword)) {
      setAuthError('Mật khẩu mới phải có ít nhất 8 ký tự, gồm cả chữ cái và số.')
      return
    }
    if (resetNewPassword !== resetConfirmPassword) {
      setAuthError('Xác nhận mật khẩu mới không khớp.')
      return
    }
    setAuthLoading(true)
    setAuthError(null)
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/auth/reset-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          email: resetEmail.trim(),
          code: resetCode.trim(),
          newPassword: resetNewPassword,
          confirmNewPassword: resetConfirmPassword,
        }),
      })
      const data = await res.json().catch(() => null)
      if (res.ok) {
        setAuthTab('LOGIN')
        setLoginIdentifier(resetEmail.trim())
        setLoginPassword('')
        setAuthMessage(data?.message || 'Đặt lại mật khẩu thành công! Tất cả phiên đăng nhập cũ đã được thu hồi an toàn. Bạn có thể đăng nhập ngay.')
      } else {
        setAuthError(data?.detail || data?.message || 'Không thể đặt lại mật khẩu. Mã xác nhận có thể đã hết hạn hoặc không đúng.')
      }
    } catch {
      setAuthError('Lỗi kết nối máy chủ khi đặt lại mật khẩu.')
    } finally {
      setAuthLoading(false)
    }
  }

  // Notification state with unread count persisted in localStorage
  const [readNotifIds, setReadNotifIds] = useState<string[]>(() => {
    try {
      const saved = localStorage.getItem('techcare_read_notifs')
      return saved ? JSON.parse(saved) : []
    } catch {
      return []
    }
  })

  const unreadCount = SYSTEM_NOTIFICATIONS.filter((n) => !readNotifIds.includes(n.id)).length

  const handleLogout = () => {
    localStorage.removeItem('tc_token')
    localStorage.removeItem('tc_user')
    setCurrentUser(null)
    setCurrentRolePath(null)
  }

  const handleLoginSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setAuthError(null)
    setAuthMessage(null)
    setAuthLoading(true)

    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          userNameOrEmail: loginIdentifier.trim(),
          password: loginPassword,
        }),
      })

      if (res.ok) {
        const data = await res.json()
        const authUser: AuthUser = {
          userId: data.user?.userId || data.userId || 1,
          userName: data.user?.userName || data.userName || loginIdentifier.trim(),
          email: data.user?.email,
          roles: data.user?.roles || data.roles || ['ADMIN'],
          permissions: data.user?.permissions || data.permissions || [],
          token: data.accessToken,
        }
        localStorage.setItem('tc_token', data.accessToken)
        localStorage.setItem('tc_user', JSON.stringify(authUser))
        setCurrentUser(authUser)
        setIsLoginOpen(false)
        if (authUser.roles.includes('ADMIN')) setCurrentRolePath('/admin')
        else if (authUser.roles.includes('RECEPTIONIST')) setCurrentRolePath('/le-tan')
        else if (authUser.roles.includes('TECHNICIAN')) setCurrentRolePath('/ky-thuat')
        else if (authUser.roles.includes('INVENTORY')) setCurrentRolePath('/kho')
        else if (authUser.roles.includes('CASHIER')) setCurrentRolePath('/thu-ngan')
        else if (authUser.roles.includes('SUPPORT')) setCurrentRolePath('/ho-tro')
        else setCurrentRolePath('/tai-khoan')
      } else {
        const err = await res.json().catch(() => null)
        const isNotVerified = err?.errorCode === 'EMAIL_NOT_VERIFIED' || err?.extensions?.errorCode === 'EMAIL_NOT_VERIFIED'
        if (isNotVerified) {
          const targetEmail = err?.extensions?.email || err?.email || loginIdentifier.trim()
          setVerifyEmail(targetEmail)
          setAuthTab('VERIFY_EMAIL')
          setAuthMessage(err?.detail || 'Tài khoản của bạn chưa xác thực email. Mã xác thực 6 số đã được gửi đến email.')
          setResendCountdown(60)
        } else {
          setAuthError(err?.detail || err?.message || 'Tên đăng nhập / Email hoặc mật khẩu không chính xác.')
        }
      }
    } catch {
      // Local fallback mock session
      const roles = loginIdentifier.toLowerCase().includes('tech')
        ? ['TECHNICIAN']
        : loginIdentifier.toLowerCase().includes('kho')
        ? ['INVENTORY']
        : loginIdentifier.toLowerCase().includes('tan')
        ? ['RECEPTIONIST']
        : loginIdentifier.toLowerCase().includes('thu')
        ? ['CASHIER']
        : loginIdentifier.toLowerCase().includes('tro')
        ? ['SUPPORT']
        : ['ADMIN']

      const mockUser: AuthUser = {
        userId: 1,
        userName: loginIdentifier.trim() || 'Admin',
        roles,
        permissions: ['ALL'],
        token: 'demo-token',
      }
      localStorage.setItem('tc_token', 'demo-token')
      localStorage.setItem('tc_user', JSON.stringify(mockUser))
      setCurrentUser(mockUser)
      setIsLoginOpen(false)
      if (mockUser.roles.includes('ADMIN')) setCurrentRolePath('/admin')
      else if (mockUser.roles.includes('RECEPTIONIST')) setCurrentRolePath('/le-tan')
      else if (mockUser.roles.includes('TECHNICIAN')) setCurrentRolePath('/ky-thuat')
      else if (mockUser.roles.includes('INVENTORY')) setCurrentRolePath('/kho')
      else if (mockUser.roles.includes('CASHIER')) setCurrentRolePath('/thu-ngan')
      else if (mockUser.roles.includes('SUPPORT')) setCurrentRolePath('/ho-tro')
      else setCurrentRolePath('/tai-khoan')
    } finally {
      setAuthLoading(false)
    }
  }

  const openNotifications = () => {
    setIsNotifOpen(true)
    const allIds = SYSTEM_NOTIFICATIONS.map((n) => n.id)
    setReadNotifIds(allIds)
    try {
      localStorage.setItem('techcare_read_notifs', JSON.stringify(allIds))
    } catch (e) {
      console.warn(e)
    }
  }

  // Close modals on Escape key
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setIsNotifOpen(false)
        setIsLoginOpen(false)
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [])

  const switchTab = (tab: TabType, issue = '') => {
    if (issue) setSelectedIssue(issue)
    setActiveTab(tab)
    window.scrollTo({ top: 0, behavior: 'instant' })
  }

  const handleNavigateToTracking = (code: string, phone: string) => {
    setTrackingQuery({ code, phone })
    setActiveTab('tracking')
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const scrollToTop = (e: React.MouseEvent) => {
    e.preventDefault()
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  // Exact 6 Monochrome Outline Line SVGs from Image 1
  const symptoms = [
    {
      title: 'Không lên nguồn',
      sub: 'Mất nguồn, chập sạc, IC nguồn',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
          <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
        </svg>
      ),
    },
    {
      title: 'Vỡ màn hình',
      sub: 'Sọc, chớp giật, tối đen hiển thị',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
          <rect x="2" y="3" width="20" height="14" rx="2" />
          <line x1="8" y1="21" x2="16" y2="21" />
          <line x1="12" y1="17" x2="12" y2="21" />
          <line x1="6" y1="6" x2="18" y2="14" />
        </svg>
      ),
    },
    {
      title: 'Pin chai / Phồng',
      sub: 'Không nhận sạc, tụt pin nhanh',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
          <rect x="2" y="7" width="16" height="10" rx="2" />
          <line x1="20" y1="11" x2="20" y2="13" />
          <line x1="6" y1="12" x2="10" y2="12" />
        </svg>
      ),
    },
    {
      title: 'Máy nóng / Kêu to',
      sub: 'Bụi bẩn, khô keo, quạt kêu rít',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
          <path d="M14 14.76V3.5a2.5 2.5 0 0 0-5 0v11.26a4.5 4.5 0 1 0 5 0z" />
        </svg>
      ),
    },
    {
      title: 'Vào nước / Đổ ẩm',
      sub: 'Cần sấy khô và xử lý oxy hóa',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
          <path d="M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0z" />
        </svg>
      ),
    },
    {
      title: 'Liệt bàn phím',
      sub: 'Chập phím, liệt hàng, kẹt nút',
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round">
          <rect x="2" y="4" width="20" height="16" rx="2" />
          <line x1="6" y1="8" x2="6.01" y2="8" />
          <line x1="10" y1="8" x2="10.01" y2="8" />
          <line x1="14" y1="8" x2="14.01" y2="8" />
          <line x1="18" y1="8" x2="18.01" y2="8" />
          <line x1="6" y1="12" x2="6.01" y2="12" />
          <line x1="18" y1="12" x2="18.01" y2="12" />
          <line x1="10" y1="16" x2="14" y2="16" />
        </svg>
      ),
    },
  ]

  const pricingItems = [
    { brand: 'ALL', service: 'Vệ sinh laptop & Tra keo tản nhiệt gốm MX-4', price: '150.000đ', time: '~30 phút', warranty: '01 tháng' },
    { brand: 'DELL', service: 'Thay pin Dell Inspiron / Vostro / XPS chính hãng', price: 'Từ 550.000đ', time: '~30 phút', warranty: '12 tháng' },
    { brand: 'MACBOOK', service: 'Thay pin Apple MacBook Air / Pro chính hãng', price: 'Từ 850.000đ', time: '~45 phút', warranty: '12 tháng' },
    { brand: 'THINKPAD', service: 'Thay bàn phím ThinkPad T480 / T490 / X1 Carbon', price: 'Từ 450.000đ', time: '~30 phút', warranty: '12 tháng' },
    { brand: 'ASUS', service: 'Thay màn hình Asus ZenBook / ROG 144Hz IPS', price: 'Từ 950.000đ', time: '~45 phút', warranty: '12 tháng' },
    { brand: 'HP', service: 'Thay bản lề & Gia cố chân ốc HP Pavilion / Envy', price: 'Từ 280.000đ', time: '~60 phút', warranty: '06 tháng' },
    { brand: 'ALL', service: 'Nâng cấp SSD NVMe PCIe Gen 4 (512GB - 1TB)', price: 'Từ 480.000đ', time: '~20 phút', warranty: '36 tháng' },
    { brand: 'ALL', service: 'Nâng cấp RAM DDR4 / DDR5 Kingston/Samsung', price: 'Từ 380.000đ', time: '~15 phút', warranty: '36 tháng' },
    { brand: 'ALL', service: 'Sửa nguồn mainboard, IC sạc Type-C, mất 3V/5V', price: 'Từ 400.000đ', time: '~120 phút', warranty: '03 tháng' },
    { brand: 'ALL', service: 'Cứu dữ liệu ổ cứng bad sector & Cài đặt hệ điều hành', price: 'Từ 150.000đ', time: '~45 phút', warranty: 'Trọn đời' },
  ]

  const filteredPricing = pricingBrand === 'ALL'
    ? pricingItems
    : pricingItems.filter(p => p.brand === pricingBrand || p.brand === 'ALL')

  if (currentUser && currentRolePath) {
    return (
      <RoleDashboard
        user={currentUser}
        initialPath={currentRolePath}
        onLogout={handleLogout}
        onNavigateHome={() => setCurrentRolePath(null)}
      />
    )
  }

  return (
    <div className="page-wrapper">
      {/* 1. STICKY TOP NAV WITH TAB SWITCHING */}
      <header className="sticky-header">
        <div className="nav-container">
          <div className="brand" onClick={() => switchTab('home')}>
            {/* Minimalist Wrench Logo */}
            <svg className="brand-wrench" viewBox="0 0 20 20">
              <path d="M14.5 1.5a4.5 4.5 0 00-4.3 5.8L1.8 15.7a1.8 1.8 0 002.5 2.5l8.4-8.4a4.5 4.5 0 005.8-4.3l-2.6 2.6-2.4-.6-.6-2.4z" />
            </svg>
            <span>TechCare</span>
          </div>

          <nav className="nav-tabs">
            <button
              type="button"
              className={`tab-btn ${activeTab === 'home' ? 'active' : ''}`}
              onClick={() => switchTab('home')}
            >
              Trang chủ
            </button>
            <button
              type="button"
              className={`tab-btn ${activeTab === 'services' ? 'active' : ''}`}
              onClick={() => switchTab('services')}
            >
              Dịch vụ
            </button>
            <button
              type="button"
              className={`tab-btn ${activeTab === 'pricing' ? 'active' : ''}`}
              onClick={() => switchTab('pricing')}
            >
              Bảng giá
            </button>
            <button
              type="button"
              className={`tab-btn ${activeTab === 'booking' ? 'active' : ''}`}
              onClick={() => switchTab('booking')}
            >
              Đặt lịch & Quét máy
            </button>
            <button
              type="button"
              className={`tab-btn ${activeTab === 'tracking' ? 'active' : ''}`}
              onClick={() => switchTab('tracking')}
            >
              Tra cứu đơn
            </button>
          </nav>

          <div className="nav-actions">
            {/* Đặt lịch pill button (direct switch to booking tab) */}
            <button
              type="button"
              className="nav-book-pill"
              onClick={() => switchTab('booking')}
            >
              Đặt lịch
            </button>

            {/* Notification Bell Icon */}
            <button
              type="button"
              className="icon-btn"
              onClick={openNotifications}
              title="Thông báo"
            >
              <svg viewBox="0 0 24 24">
                <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
                <path d="M13.73 21a2 2 0 0 1-3.46 0" />
              </svg>
              {unreadCount > 0 && <span className="notif-badge-count">{unreadCount}</span>}
            </button>

            {/* User Login Icon */}
            <button
              type="button"
              className="icon-btn"
              onClick={() => setIsLoginOpen(true)}
              title="Đăng nhập tài khoản"
            >
              <svg viewBox="0 0 24 24">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                <circle cx="12" cy="7" r="4" />
              </svg>
            </button>
          </div>
        </div>
      </header>

      {/* 2. TAB CONTENT: TRANG CHỦ */}
      {activeTab === 'home' && (
        <main>
          {/* Hero Section */}
          <section className="hero">
            <KindredCanvas />

            <div className="hero-copy">
              <div className="status-badge">
                <span className="status-dot-pcb" style={{ background: '#10b981', boxShadow: '0 0 0 0 rgba(16, 185, 129, 0.7)' }}></span>
                <span>Đang mở cửa · Nhận máy đến 21:00</span>
              </div>

              <h1>
                <span className="soft">Giải cứu</span>
                chiếc laptop của bạn.
              </h1>

              <p className="hero-subline">
                Chẩn đoán miễn phí trong 15 phút. Sửa tận gốc, linh kiện chính hãng, bảo hành đến 12 tháng.
              </p>

              <div className="cta-group">
                <button
                  type="button"
                  className="primary-pill-cta"
                  onClick={() => switchTab('booking')}
                >
                  Đặt lịch sửa ngay <span>→</span>
                </button>
                <button
                  type="button"
                  className="secondary-cta"
                  onClick={() => switchTab('tracking')}
                >
                  Tra cứu đơn sửa →
                </button>
              </div>
            </div>

            {/* Perks Row (6 cam kết) */}
            <div className="perks-row">
              <div className="perk-item">
                <svg viewBox="0 0 24 24">
                  <path d="M4.8 2.3A.3.3 0 1 0 5 2H4a2 2 0 0 0-2 2v5a6 6 0 0 0 6 6v0a6 6 0 0 0 6-6V4a2 2 0 0 0-2-2h-1a.2.2 0 1 0 .3.3" />
                  <path d="M8 15v1a6 6 0 0 0 6 6v0a6 6 0 0 0 6-6v-4" />
                  <circle cx="20" cy="10" r="2" />
                </svg>
                <span>Chẩn đoán miễn phí</span>
              </div>

              <div className="perk-item">
                <svg viewBox="0 0 24 24">
                  <rect x="4" y="4" width="16" height="16" rx="2" />
                  <rect x="9" y="9" width="6" height="6" />
                  <path d="M9 1v3M15 1v3M9 20v3M15 20v3M20 9h3M20 14h3M1 9h3M1 14h3" />
                </svg>
                <span>Linh kiện chính hãng</span>
              </div>

              <div className="perk-item">
                <svg viewBox="0 0 24 24">
                  <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                  <path d="m9 12 2 2 4-4" />
                </svg>
                <span>Bảo hành 12 tháng</span>
              </div>

              <div className="perk-item">
                <svg viewBox="0 0 24 24">
                  <circle cx="12" cy="12" r="10" />
                  <polyline points="12 6 12 12 16 14" />
                </svg>
                <span>Lấy ngay trong ngày</span>
              </div>

              <div className="perk-item">
                <svg viewBox="0 0 24 24">
                  <rect x="1" y="3" width="15" height="13" />
                  <polygon points="16 8 20 8 23 11 23 16 16 16 8" />
                  <circle cx="5.5" cy="18.5" r="2.5" />
                  <circle cx="18.5" cy="18.5" r="2.5" />
                </svg>
                <span>Nhận máy tận nơi</span>
              </div>

              <div className="perk-item">
                <svg viewBox="0 0 24 24">
                  <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
                  <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                </svg>
                <span>Bảo mật dữ liệu</span>
              </div>
            </div>
          </section>

          {/* 3. LỖI THƯỜNG GẶP (Exact recreation from Image 1) */}
          <section className="symptoms-section section-pad">
            <div className="section-heading">
              <div>
                <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '8px' }}>
                  CHẨN ĐOÁN NHANH
                </div>
                <h2>Máy bạn đang gặp lỗi gì?</h2>
              </div>
              <p className="section-intro">
                Chọn sự cố đang gặp phải để nhận tư vấn phương án xử lý tức thì và chuẩn bị linh kiện trước khi mang máy tới.
              </p>
            </div>

            <div className="symptoms-grid">
              {symptoms.map((s) => (
                <div
                  key={s.title}
                  className="symptom-card"
                  onClick={() => switchTab('booking', `Lỗi: ${s.title} (${s.sub})`)}
                >
                  <div className="symptom-svg">{s.icon}</div>
                  <div className="symptom-title">{s.title}</div>
                  <div className="symptom-sub">{s.sub}</div>
                </div>
              ))}
            </div>
          </section>

          {/* 4. PHOTO 2 EXACT RECREATION: DỊCH VỤ ("Máy cần gì, Mạch chăm nấy") */}
          <section className="services-section section-pad">
            <div className="section-heading">
              <div>
                <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '8px' }}>
                  DỊCH VỤ
                </div>
                <h2>
                  Máy cần gì, <br />
                  <span className="soft">Mạch chăm nấy.</span>
                </h2>
              </div>
              <p className="section-intro">
                Từ một chiếc máy chạy chậm đến sự cố phần cứng — bắt đầu bằng kiểm tra và tư vấn rõ ràng.
              </p>
            </div>

            <div className="service-grid-photo2">
              <div className="service-card-photo2" onClick={() => switchTab('booking', 'Màn hình & bản lề')}>
                <div className="photo2-card-no">01 / HIỂN THỊ</div>
                <div className="photo2-line-icon">
                  <svg viewBox="0 0 24 24">
                    <rect x="2" y="3" width="20" height="14" rx="2" />
                    <line x1="8" y1="21" x2="16" y2="21" />
                    <line x1="12" y1="17" x2="12" y2="21" />
                  </svg>
                </div>
                <h3>Màn hình & bản lề</h3>
                <p>Sọc, nhòe, vỡ màn hình hoặc bản lề lỏng — kiểm tra nguyên nhân trước khi thay.</p>
                <span className="photo2-card-arrow">↗</span>
              </div>

              <div className="service-card-photo2" onClick={() => switchTab('booking', 'Pin & sạc')}>
                <div className="photo2-card-no">02 / NGUỒN</div>
                <div className="photo2-line-icon">
                  <svg viewBox="0 0 24 24">
                    <rect x="2" y="7" width="16" height="10" rx="2" />
                    <line x1="20" y1="11" x2="20" y2="13" />
                    <path d="m10 9-2 3h4l-2 3" />
                  </svg>
                </div>
                <h3>Pin & sạc</h3>
                <p>Pin chai, máy không nhận sạc hay tự tắt — tìm đúng điểm lỗi, tư vấn phương án phù hợp.</p>
                <span className="photo2-card-arrow">↗</span>
              </div>

              <div className="service-card-photo2" onClick={() => switchTab('booking', 'Vệ sinh & nâng cấp')}>
                <div className="photo2-card-no">03 / HIỆU NĂNG</div>
                <div className="photo2-line-icon">
                  <svg viewBox="0 0 24 24">
                    <rect x="4" y="4" width="16" height="16" rx="2" />
                    <path d="M4 9h16M4 15h16M9 4v16M15 4v16" />
                  </svg>
                </div>
                <h3>Vệ sinh & nâng cấp</h3>
                <p>Làm sạch bụi, kiểm tra tản nhiệt, tư vấn nâng RAM hoặc SSD theo nhu cầu sử dụng.</p>
                <span className="photo2-card-arrow">↗</span>
              </div>

              <div className="service-card-photo2" onClick={() => switchTab('booking', 'Lỗi hệ điều hành')}>
                <div className="photo2-card-no">04 / PHẦN MỀM</div>
                <div className="photo2-line-icon">
                  <svg viewBox="0 0 24 24">
                    <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
                    <polyline points="3.27 6.96 12 12.01 20.73 6.96" />
                    <line x1="12" y1="22.08" x2="12" y2="12" />
                  </svg>
                </div>
                <h3>Lỗi hệ điều hành</h3>
                <p>Khởi động lỗi, treo máy, phần mềm bất ổn — hỗ trợ sao lưu và cài đặt theo nhu cầu.</p>
                <span className="photo2-card-arrow">↗</span>
              </div>
            </div>

            <div className="photo2-service-footnote">
              * Chưa rõ máy bị gì? Cứ mô tả tình trạng, Mạch sẽ tư vấn bước tiếp theo.
            </div>
          </section>

          {/* 5. PHOTO 3 EXACT RECREATION: CÁCH MẠCH LÀM VIỆC ("Không có gì mập mờ.") */}
          <section className="process-section-photo3 section-pad">
            <div className="photo3-left-head">
              <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '8px' }}>
                CÁCH MẠCH LÀM VIỆC
              </div>
              <h2>
                Không có gì <br />
                <span className="soft">mập mờ.</span>
              </h2>
            </div>

            <div className="photo3-steps-container">
              <div className="photo3-step-row">
                <div className="photo3-step-number">01</div>
                <div className="photo3-step-content">
                  <h3>Lắng nghe & kiểm tra</h3>
                  <p>Bạn mô tả tình trạng. Mạch kiểm tra máy và xác định các khả năng gây lỗi.</p>
                </div>
              </div>

              <div className="photo3-step-row">
                <div className="photo3-step-number">02</div>
                <div className="photo3-step-content">
                  <h3>Báo phương án, báo giá</h3>
                  <p>Giải thích tình trạng và chi phí dự kiến. Chỉ tiến hành khi bạn đồng ý.</p>
                </div>
              </div>

              <div className="photo3-step-row">
                <div className="photo3-step-number">03</div>
                <div className="photo3-step-content">
                  <h3>Sửa, thử máy, bàn giao</h3>
                  <p>Kiểm tra lại các chức năng liên quan, hướng dẫn bảo hành khi nhận máy.</p>
                </div>
              </div>
            </div>
          </section>

          {/* Brands Supported */}
          <section className="brands-section section-pad">
            <div className="brands-row">
              <span>DELL</span>
              <span>APPLE MACBOOK</span>
              <span>LENOVO THINKPAD</span>
              <span>ASUS ROG</span>
              <span>HP VICTUS</span>
              <span>ACER PREDATOR</span>
              <span>MSI</span>
              <span>LG GRAM</span>
            </div>
          </section>

          {/* Reviews */}
          <section className="reviews-section section-pad">
            <div className="section-heading">
              <div>
                <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '8px' }}>
                  ĐÁNH GIÁ KHÁCH HÀNG
                </div>
                <h2>Tin tưởng từ trải nghiệm thật.</h2>
              </div>
              <p className="section-intro">
                Hơn 25.000 khách hàng đã phục hồi thiết bị làm việc của họ tại xưởng TechCare.
              </p>
            </div>

            <div className="reviews-grid" style={{ marginBottom: 0 }}>
              <div className="review-card">
                <div>
                  <div className="stars">★★★★★</div>
                  <p>
                    "ThinkPad X1 của mình bị sập nguồn khi cắm sạc. Mang ra thợ đo IC nguồn và xử lý lấy liền trong 1 tiếng rưỡi, được ngồi xem trực tiếp. Rất minh bạch!"
                  </p>
                </div>
                <div className="review-author">
                  <strong>Hoàng Nam</strong>
                  <span>ThinkPad X1 Carbon Gen 9 — Sửa nguồn</span>
                </div>
              </div>

              <div className="review-card">
                <div>
                  <div className="stars">★★★★★</div>
                  <p>
                    "Thay màn hình Dell XPS 13 lấy ngay trong 40 phút. Màn sáng đều, chuẩn màu, thợ mở máy cho ký tên từng linh kiện rất yên tâm."
                  </p>
                </div>
                <div className="review-author">
                  <strong>Thu Hà</strong>
                  <span>Dell XPS 13 9310 — Thay màn hình</span>
                </div>
              </div>

              <div className="review-card">
                <div>
                  <div className="stars">★★★★★</div>
                  <p>
                    "MacBook Pro M1 bị vào nước cà phê tưởng phải thay cả cụm mainboard hãng báo mười mấy triệu, qua TechCare được cứu sống với giá rất hợp lý."
                  </p>
                </div>
                <div className="review-author">
                  <strong>Minh Triết</strong>
                  <span>MacBook Pro 14" M1 — Cứu máy vào nước</span>
                </div>
              </div>
            </div>
          </section>
        </main>
      )}

      {/* 3. TAB CONTENT: DỊCH VỤ CHI TIẾT */}
      {activeTab === 'services' && (
        <main className="tab-page-container">
          <div className="tab-page-header">
            <div className="eyebrow">CHUYÊN KHOA PHẦN CỨNG & PHẦN MỀM</div>
            <h1>Dịch vụ sửa chữa chuyên sâu.</h1>
            <p>
              Tất cả các dịch vụ đều được bảo hành bằng tem điện tử và mã tra cứu online. Khách hàng quan sát trực tiếp toàn bộ quá trình mở máy.
            </p>
          </div>

          <div className="service-grid-photo2" style={{ marginBottom: '40px' }}>
            <div className="service-card-photo2" onClick={() => switchTab('booking', 'Màn hình & bản lề')}>
              <div className="photo2-card-no">01 / HIỂN THỊ</div>
              <div className="photo2-line-icon">
                <svg viewBox="0 0 24 24">
                  <rect x="2" y="3" width="20" height="14" rx="2" />
                  <line x1="8" y1="21" x2="16" y2="21" />
                  <line x1="12" y1="17" x2="12" y2="21" />
                </svg>
              </div>
              <h3>Màn hình & bản lề</h3>
              <p>Sọc, nhòe, vỡ màn hình hoặc bản lề lỏng — kiểm tra nguyên nhân trước khi thay.</p>
              <div style={{ marginTop: '12px', fontSize: '12px', fontWeight: 600, color: '#111' }}>Giá từ: 950.000đ · Lấy trong 45p</div>
              <span className="photo2-card-arrow">↗</span>
            </div>

            <div className="service-card-photo2" onClick={() => switchTab('booking', 'Pin & sạc')}>
              <div className="photo2-card-no">02 / NGUỒN</div>
              <div className="photo2-line-icon">
                <svg viewBox="0 0 24 24">
                  <rect x="2" y="7" width="16" height="10" rx="2" />
                  <line x1="20" y1="11" x2="20" y2="13" />
                  <path d="m10 9-2 3h4l-2 3" />
                </svg>
              </div>
              <h3>Pin & sạc</h3>
              <p>Pin chai, máy không nhận sạc hay tự tắt — tìm đúng điểm lỗi, tư vấn phương án phù hợp.</p>
              <div style={{ marginTop: '12px', fontSize: '12px', fontWeight: 600, color: '#111' }}>Giá từ: 550.000đ · Lấy trong 30p</div>
              <span className="photo2-card-arrow">↗</span>
            </div>

            <div className="service-card-photo2" onClick={() => switchTab('booking', 'Vệ sinh & nâng cấp')}>
              <div className="photo2-card-no">03 / HIỆU NĂNG</div>
              <div className="photo2-line-icon">
                <svg viewBox="0 0 24 24">
                  <rect x="4" y="4" width="16" height="16" rx="2" />
                  <path d="M4 9h16M4 15h16M9 4v16M15 4v16" />
                </svg>
              </div>
              <h3>Vệ sinh & nâng cấp</h3>
              <p>Làm sạch bụi, kiểm tra tản nhiệt, tư vấn nâng RAM hoặc SSD theo nhu cầu sử dụng.</p>
              <div style={{ marginTop: '12px', fontSize: '12px', fontWeight: 600, color: '#111' }}>Giá từ: 150.000đ · Lấy trong 30p</div>
              <span className="photo2-card-arrow">↗</span>
            </div>

            <div className="service-card-photo2" onClick={() => switchTab('booking', 'Lỗi hệ điều hành')}>
              <div className="photo2-card-no">04 / PHẦN MỀM</div>
              <div className="photo2-line-icon">
                <svg viewBox="0 0 24 24">
                  <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z" />
                  <polyline points="3.27 6.96 12 12.01 20.73 6.96" />
                  <line x1="12" y1="22.08" x2="12" y2="12" />
                </svg>
              </div>
              <h3>Lỗi hệ điều hành</h3>
              <p>Khởi động lỗi, treo máy, phần mềm bất ổn — hỗ trợ sao lưu và cài đặt theo nhu cầu.</p>
              <div style={{ marginTop: '12px', fontSize: '12px', fontWeight: 600, color: '#111' }}>Giá từ: 150.000đ · Lấy trong 45p</div>
              <span className="photo2-card-arrow">↗</span>
            </div>
          </div>

          <div style={{ textAlign: 'center', marginTop: '20px' }}>
            <button type="button" className="button" onClick={() => switchTab('booking')}>
              Đặt lịch kiểm tra trực tiếp tại tiệm <span>→</span>
            </button>
          </div>
        </main>
      )}

      {/* 4. TAB CONTENT: BẢNG GIÁ MINH BẠCH */}
      {activeTab === 'pricing' && (
        <main className="tab-page-container">
          <div className="tab-page-header">
            <div className="eyebrow">BẢNG GIÁ THAM KHẢO</div>
            <h1>Báo giá công khai, không chi phí ẩn.</h1>
            <p>
              Kỹ thuật viên báo giá trọn gói bao gồm công thợ và bảo hành 1 đổi 1 trước khi tiến hành. Khách hàng duyệt giá mới làm.
            </p>
          </div>

          <div className="filter-bar">
            {['ALL', 'DELL', 'MACBOOK', 'THINKPAD', 'ASUS', 'HP'].map((brand) => (
              <button
                key={brand}
                type="button"
                className={`filter-pill ${pricingBrand === brand ? 'active' : ''}`}
                onClick={() => setPricingBrand(brand)}
              >
                {brand === 'ALL' ? 'Tất cả dòng máy' : brand}
              </button>
            ))}
          </div>

          <div className="pricing-table-wrap">
            <table className="pricing-table">
              <thead>
                <tr>
                  <th>Hạng mục dịch vụ</th>
                  <th>Chi phí tham khảo</th>
                  <th>Thời gian xử lý</th>
                  <th>Chính sách bảo hành</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {filteredPricing.map((item, idx) => (
                  <tr key={idx}>
                    <td><strong>{item.service}</strong></td>
                    <td><span style={{ fontWeight: 600 }}>{item.price}</span></td>
                    <td style={{ color: '#666' }}>{item.time}</td>
                    <td>{item.warranty}</td>
                    <td>
                      <button
                        type="button"
                        className="pricing-book-btn"
                        onClick={() => switchTab('booking', item.service)}
                      >
                        Đặt lịch
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </main>
      )}

      {/* 5. TAB CONTENT: ĐẶT LỊCH & QUÉT CẤU HÌNH 1-CLICK (Photo 1) */}
      {activeTab === 'booking' && (
        <main className="tab-page-container">
          <BookingSection selectedIssue={selectedIssue} onNavigateToTracking={handleNavigateToTracking} />
        </main>
      )}

      {/* 6. TAB CONTENT: TRA CỨU TIẾN TRÌNH */}
      {activeTab === 'tracking' && (
        <main className="tab-page-container">
          <TrackingSection initialCode={trackingQuery?.code} initialPhone={trackingQuery?.phone} autoSearch={Boolean(trackingQuery?.code)} />
        </main>
      )}

      {/* 7. FOOTER */}
      <footer className="footer">
        <div className="footer-caption">
          <strong>TechCare</strong> — Chăm sóc máy tính chuẩn chuyên gia · Hotline: 0909 123 456 · 8:00 - 21:00 hàng ngày
        </div>
        <a href="#" className="back-top" onClick={scrollToTop}>
          Về đầu trang ↑
        </a>
      </footer>

      {/* 8. NOTIFICATION MODAL */}
      {isNotifOpen && (
        <div className="modal-backdrop" onClick={() => setIsNotifOpen(false)}>
          <div className="modal-card notif-modal-card" style={{ maxWidth: '480px' }} onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div>
                <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '4px' }}>HỆ THỐNG TECHCARE</div>
                <h3>Thông báo hệ thống</h3>
              </div>
              <button type="button" className="close-btn" onClick={() => setIsNotifOpen(false)}>
                ✕
              </button>
            </div>
            <div className="notif-modal-list">
              {SYSTEM_NOTIFICATIONS.map((notif) => {
                const tagClass =
                  notif.type === 'OPENING_HOURS'
                    ? 'notif-tag-hours'
                    : notif.type === 'UPDATE'
                    ? 'notif-tag-update'
                    : 'notif-tag-promo'

                const tagLabel =
                  notif.type === 'OPENING_HOURS'
                    ? 'Giờ hoạt động'
                    : notif.type === 'UPDATE'
                    ? 'Bản nâng cấp'
                    : 'Ưu đãi'

                return (
                  <div key={notif.id} className="notif-card-item">
                    <div className="notif-item-header">
                      <span className={`notif-tag ${tagClass}`}>{tagLabel}</span>
                      <span className="notif-time">{notif.time}</span>
                    </div>
                    <h4 style={{ fontSize: '13.5px', fontWeight: 600, color: 'var(--ink)', marginBottom: '4px' }}>
                      {notif.title}
                    </h4>
                    <p style={{ fontSize: '12px', color: '#555', lineHeight: 1.5, margin: '4px 0 8px' }}>
                      {notif.content}
                    </p>
                    {notif.action && (
                      <button
                        type="button"
                        className="notif-action-btn"
                        onClick={() => {
                          switchTab(notif.action!.tab)
                          setIsNotifOpen(false)
                        }}
                      >
                        {notif.action.label} <span>→</span>
                      </button>
                    )}
                  </div>
                )
              })}
            </div>
          </div>
        </div>
      )}

      {/* 9. LOGIN / REGISTER / AUTH MODAL */}
      {isLoginOpen && (
        <div className="modal-backdrop" onClick={() => setIsLoginOpen(false)}>
          <div className="modal-card" style={{ maxWidth: '440px' }} onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div>
                <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '4px' }}>HỆ THỐNG TECHCARE</div>
                <h3>
                  {authTab === 'LOGIN' && 'Đăng nhập'}
                  {authTab === 'REGISTER' && 'Đăng ký tài khoản'}
                  {authTab === 'VERIFY_EMAIL' && 'Xác thực Email'}
                  {authTab === 'FORGOT_PASSWORD' && 'Quên mật khẩu'}
                  {authTab === 'RESET_PASSWORD' && 'Đặt lại mật khẩu'}
                </h3>
              </div>
              <button
                type="button"
                className="close-btn"
                onClick={() => {
                  setIsLoginOpen(false)
                  setAuthError(null)
                  setAuthMessage(null)
                }}
              >
                ✕
              </button>
            </div>

            {/* Tab Switcher: Only on Login / Register */}
            {(authTab === 'LOGIN' || authTab === 'REGISTER') && (
              <div style={{ display: 'flex', borderBottom: '1px solid var(--line)', marginBottom: '16px' }}>
                <button
                  type="button"
                  onClick={() => { setAuthTab('LOGIN'); setAuthError(null); setAuthMessage(null); }}
                  style={{
                    flex: 1,
                    padding: '10px 0',
                    border: 'none',
                    background: 'none',
                    fontSize: '13.5px',
                    fontWeight: authTab === 'LOGIN' ? 600 : 400,
                    color: authTab === 'LOGIN' ? 'var(--ink)' : 'var(--sub)',
                    borderBottom: authTab === 'LOGIN' ? '2px solid var(--ink)' : '2px solid transparent',
                    cursor: 'pointer',
                  }}
                >
                  Đăng nhập
                </button>
                <button
                  type="button"
                  onClick={() => { setAuthTab('REGISTER'); setAuthError(null); setAuthMessage(null); }}
                  style={{
                    flex: 1,
                    padding: '10px 0',
                    border: 'none',
                    background: 'none',
                    fontSize: '13.5px',
                    fontWeight: authTab === 'REGISTER' ? 600 : 400,
                    color: authTab === 'REGISTER' ? 'var(--ink)' : 'var(--sub)',
                    borderBottom: authTab === 'REGISTER' ? '2px solid var(--ink)' : '2px solid transparent',
                    cursor: 'pointer',
                  }}
                >
                  Đăng ký tài khoản
                </button>
              </div>
            )}

            {/* Error & Success Alerts */}
            {authError && (
              <div style={{ color: '#dc2626', background: '#fef2f2', padding: '10px 14px', borderRadius: '8px', fontSize: '12.5px', marginBottom: '14px', border: '1px solid #fecaca' }}>
                {authError}
              </div>
            )}
            {authMessage && (
              <div style={{ color: '#065f46', background: '#ecfdf5', padding: '10px 14px', borderRadius: '8px', fontSize: '12.5px', marginBottom: '14px', border: '1px solid #a7f3d0' }}>
                {authMessage}
              </div>
            )}

            {/* Form 1: LOGIN */}
            {authTab === 'LOGIN' && (
              <form onSubmit={handleLoginSubmit} className="booking-form">
                <div>
                  <label>Tên đăng nhập hoặc Email</label>
                  <input
                    type="text"
                    required
                    placeholder="Email khách hàng hoặc user nhân viên (admin / le-tan...)"
                    value={loginIdentifier}
                    onChange={(e) => setLoginIdentifier(e.target.value)}
                    autoFocus
                  />
                </div>
                <div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <label style={{ margin: 0 }}>Mật khẩu</label>
                    <button
                      type="button"
                      onClick={() => { setAuthTab('FORGOT_PASSWORD'); setAuthError(null); setAuthMessage(null); }}
                      style={{ background: 'none', border: 'none', fontSize: '12px', color: 'var(--sub)', cursor: 'pointer', textDecoration: 'underline' }}
                    >
                      Quên mật khẩu?
                    </button>
                  </div>
                  <input
                    type="password"
                    required
                    placeholder="••••••••"
                    value={loginPassword}
                    onChange={(e) => setLoginPassword(e.target.value)}
                  />
                </div>

                <div style={{ marginTop: '14px' }}>
                  <button type="submit" className="button" style={{ width: '100%' }} disabled={authLoading}>
                    {authLoading ? 'Đang xác thực...' : 'Đăng nhập vào hệ thống'} <span>→</span>
                  </button>
                </div>
              </form>
            )}

            {/* Form 2: REGISTER */}
            {authTab === 'REGISTER' && (
              <form onSubmit={handleRegisterSubmit} className="booking-form">
                <div>
                  <label>Họ và tên *</label>
                  <input
                    type="text"
                    required
                    placeholder="Nguyễn Văn A"
                    value={regFullName}
                    onChange={(e) => setRegFullName(e.target.value)}
                    autoFocus
                  />
                </div>
                <div>
                  <label>Email liên hệ & đăng nhập *</label>
                  <input
                    type="email"
                    required
                    placeholder="example@domain.com"
                    value={regEmail}
                    onChange={(e) => setRegEmail(e.target.value)}
                  />
                </div>
                <div>
                  <label>Số điện thoại liên hệ *</label>
                  <input
                    type="tel"
                    required
                    placeholder="0912345678 (10 số, không cần OTP)"
                    value={regPhone}
                    onChange={(e) => setRegPhone(e.target.value)}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--sub)', marginTop: '2px', display: 'block' }}>
                    Số điện thoại dùng để nhận diện hồ sơ máy sửa và liên hệ giao nhận, không cần OTP.
                  </span>
                </div>
                <div>
                  <label>Mật khẩu (tối thiểu 8 ký tự, có chữ và số) *</label>
                  <input
                    type="password"
                    required
                    placeholder="••••••••"
                    value={regPassword}
                    onChange={(e) => setRegPassword(e.target.value)}
                  />
                </div>
                <div>
                  <label>Nhập lại mật khẩu *</label>
                  <input
                    type="password"
                    required
                    placeholder="••••••••"
                    value={regConfirmPassword}
                    onChange={(e) => setRegConfirmPassword(e.target.value)}
                  />
                </div>

                <div style={{ marginTop: '14px' }}>
                  <button type="submit" className="button" style={{ width: '100%' }} disabled={authLoading}>
                    {authLoading ? 'Đang tạo tài khoản...' : 'Đăng ký tài khoản'} <span>→</span>
                  </button>
                </div>
              </form>
            )}

            {/* Form 3: VERIFY EMAIL */}
            {authTab === 'VERIFY_EMAIL' && (
              <form onSubmit={handleVerifyEmailSubmit} className="booking-form">
                <p style={{ fontSize: '13px', color: '#555', lineHeight: 1.5, marginBottom: '12px' }}>
                  Mã xác thực 6 số đã được gửi tới <strong>{verifyEmail}</strong>. Vui lòng nhập mã để kích hoạt tài khoản.
                </p>
                <div>
                  <label style={{ textAlign: 'center', display: 'block' }}>Mã xác thực 6 chữ số</label>
                  <input
                    type="text"
                    required
                    maxLength={6}
                    placeholder="123456"
                    value={verifyCode}
                    onChange={(e) => setVerifyCode(e.target.value.replace(/\D/g, ''))}
                    style={{ textAlign: 'center', fontSize: '20px', letterSpacing: '6px', fontWeight: 700 }}
                    autoFocus
                  />
                </div>

                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '12.5px', marginTop: '8px' }}>
                  <button
                    type="button"
                    onClick={() => { setAuthTab('LOGIN'); setAuthError(null); setAuthMessage(null); }}
                    style={{ background: 'none', border: 'none', color: 'var(--sub)', cursor: 'pointer', textDecoration: 'underline' }}
                  >
                    ← Quay lại đăng nhập
                  </button>
                  {resendCountdown > 0 ? (
                    <span style={{ color: 'var(--sub)' }}>Gửi lại sau <strong>{resendCountdown}s</strong></span>
                  ) : (
                    <button
                      type="button"
                      onClick={handleResendEmailCode}
                      disabled={authLoading}
                      style={{ background: 'none', border: 'none', color: 'var(--ink)', fontWeight: 600, cursor: 'pointer', textDecoration: 'underline' }}
                    >
                      Gửi lại mã
                    </button>
                  )}
                </div>

                <div style={{ marginTop: '16px' }}>
                  <button type="submit" className="button" style={{ width: '100%' }} disabled={authLoading || verifyCode.length !== 6}>
                    {authLoading ? 'Đang xác thực...' : 'Xác thực & Vào hệ thống'} <span>→</span>
                  </button>
                </div>
              </form>
            )}

            {/* Form 4: FORGOT PASSWORD */}
            {authTab === 'FORGOT_PASSWORD' && (
              <form onSubmit={handleForgotPasswordSubmit} className="booking-form">
                <p style={{ fontSize: '13px', color: '#555', lineHeight: 1.5, marginBottom: '12px' }}>
                  Nhập địa chỉ email đăng ký để nhận mã 6 số khôi phục mật khẩu.
                </p>
                <div>
                  <label>Email tài khoản</label>
                  <input
                    type="email"
                    required
                    placeholder="example@domain.com"
                    value={forgotEmail}
                    onChange={(e) => setForgotEmail(e.target.value)}
                    autoFocus
                  />
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-start', marginTop: '8px' }}>
                  <button
                    type="button"
                    onClick={() => { setAuthTab('LOGIN'); setAuthError(null); setAuthMessage(null); }}
                    style={{ background: 'none', border: 'none', fontSize: '12px', color: 'var(--sub)', cursor: 'pointer', textDecoration: 'underline' }}
                  >
                    ← Quay lại đăng nhập
                  </button>
                </div>

                <div style={{ marginTop: '16px' }}>
                  <button type="submit" className="button" style={{ width: '100%' }} disabled={authLoading}>
                    {authLoading ? 'Đang gửi...' : 'Gửi mã khôi phục mật khẩu'} <span>→</span>
                  </button>
                </div>
              </form>
            )}

            {/* Form 5: RESET PASSWORD */}
            {authTab === 'RESET_PASSWORD' && (
              <form onSubmit={handleResetPasswordSubmit} className="booking-form">
                <p style={{ fontSize: '13px', color: '#555', lineHeight: 1.5, marginBottom: '12px' }}>
                  Nhập mã xác thực 6 số gửi tới <strong>{resetEmail}</strong> và đặt mật khẩu mới.
                </p>
                <div>
                  <label>Mã xác thực 6 chữ số</label>
                  <input
                    type="text"
                    required
                    maxLength={6}
                    placeholder="123456"
                    value={resetCode}
                    onChange={(e) => setResetCode(e.target.value.replace(/\D/g, ''))}
                    style={{ textAlign: 'center', fontSize: '18px', letterSpacing: '6px', fontWeight: 700 }}
                    autoFocus
                  />
                </div>
                <div>
                  <label>Mật khẩu mới (tối thiểu 8 ký tự, có chữ và số)</label>
                  <input
                    type="password"
                    required
                    placeholder="••••••••"
                    value={resetNewPassword}
                    onChange={(e) => setResetNewPassword(e.target.value)}
                  />
                </div>
                <div>
                  <label>Nhập lại mật khẩu mới</label>
                  <input
                    type="password"
                    required
                    placeholder="••••••••"
                    value={resetConfirmPassword}
                    onChange={(e) => setResetConfirmPassword(e.target.value)}
                  />
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-start', marginTop: '8px' }}>
                  <button
                    type="button"
                    onClick={() => { setAuthTab('LOGIN'); setAuthError(null); setAuthMessage(null); }}
                    style={{ background: 'none', border: 'none', fontSize: '12px', color: 'var(--sub)', cursor: 'pointer', textDecoration: 'underline' }}
                  >
                    ← Quay lại đăng nhập
                  </button>
                </div>

                <div style={{ marginTop: '16px' }}>
                  <button type="submit" className="button" style={{ width: '100%' }} disabled={authLoading}>
                    {authLoading ? 'Đang xử lý...' : 'Đặt lại mật khẩu & Đăng nhập'} <span>→</span>
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}

      {/* Floating 24/7 AI Chatbot & RAG Widget */}
      <ChatWidget />
    </div>
  )
}
