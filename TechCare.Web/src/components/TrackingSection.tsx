import { useState } from 'react'
import { API_BASE_URL } from '../api/client'

interface TicketInfo {
  code: string
  customerName: string
  phone: string
  modelName: string
  status: string
  scheduledAt: string
  issue: string
}

export function TrackingSection() {
  const [code, setCode] = useState('')
  const [phone, setPhone] = useState('')
  const [loading, setLoading] = useState(false)
  const [ticket, setTicket] = useState<TicketInfo | null>(null)
  const [searched, setSearched] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  const handleLookup = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!code.trim() || !phone.trim()) {
      setErrorMessage('Vui lòng nhập cả Mã phiếu/đơn và Số điện thoại để tra cứu bảo mật.')
      setSearched(true)
      return
    }

    setLoading(true)
    setSearched(true)
    setTicket(null)
    setErrorMessage(null)

    try {
      // Safe, dedicated tracking endpoint (requires both Code and Phone for data privacy)
      const queryParams = new URLSearchParams({
        code: code.trim(),
        phone: phone.trim()
      })
      const res = await fetch(`${API_BASE_URL}/api/v1/appointments/track?${queryParams.toString()}`)
      if (res.ok) {
        const result = await res.json()
        if (result && result.found && result.data) {
          const found = result.data
          setTicket({
            code: found.appointmentCode || found.ticketCode || code.trim().toUpperCase(),
            customerName: found.customerName || 'Khách hàng',
            phone: found.customerPhone || phone.trim(),
            modelName: found.modelName || 'Laptop',
            status: found.status || 'BOOKED',
            scheduledAt: found.scheduledAtUtc ? new Date(found.scheduledAtUtc).toLocaleString('vi-VN') : 'Đang xử lý',
            issue: found.customerDescription || found.subject || found.customerIssue || 'Kiểm tra phần cứng tổng thể',
          })
        } else {
          setErrorMessage(result.message || 'Không tìm thấy thông tin đơn hẹn hoặc phiếu sửa chữa phù hợp.')
        }
      } else {
        const err = await res.json().catch(() => null)
        setErrorMessage(err?.message || 'Không thể tra cứu vào lúc này. Vui lòng kiểm tra lại thông tin.')
      }
    } catch {
      setErrorMessage('Lỗi kết nối đến máy chủ. Vui lòng kiểm tra lại mạng hoặc thử lại sau.')
    } finally {
      setLoading(false)
    }
  }

  const steps = [
    { key: 'BOOKED', label: '1. Đã đặt hẹn', desc: 'Tiếp nhận yêu cầu trên hệ thống' },
    { key: 'RECEIVED', label: '2. Đã nhận máy', desc: 'Lập biên bản ngoại quan tại quầy' },
    { key: 'DIAGNOSING', label: '3. Đang kiểm tra', desc: 'Kỹ sư đo đạc xác định nguyên nhân' },
    { key: 'REPAIRING', label: '4. Đang sửa chữa', desc: 'Khách đã duyệt giá, đang thay thế' },
    { key: 'READY', label: '5. Sẵn sàng bàn giao', desc: 'Đã test kiểm chuẩn và dán tem BH' },
  ]

  const getStepIndex = (status: string) => {
    const s = status.toUpperCase()
    if (s.includes('READY') || s.includes('DELIVERED') || s.includes('COMPLETED') || s.includes('RETURNED')) return 4
    if (s.includes('REPAIR')) return 3
    if (s.includes('DIAGNOS') || s.includes('WAITING')) return 2
    if (s.includes('RECEIV')) return 1
    return 0
  }

  const currentIdx = ticket ? getStepIndex(ticket.status) : 0

  return (
    <section id="tracking" className="tracking-section section-pad">
      <div className="section-heading">
        <div>
          <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '8px' }}>
            MINH BẠCH TIẾN TRÌNH
          </div>
          <h2>Tra cứu đơn sửa & Bảo hành.</h2>
        </div>
        <p className="section-intro">
          Nhập mã phiếu hẹn hoặc mã sửa chữa cùng số điện thoại đăng ký của bạn để theo dõi tiến trình trực tiếp theo thời gian thực.
        </p>
      </div>

      <form className="tracking-search-bar" onSubmit={handleLookup} style={{ flexWrap: 'wrap' }}>
        <input
          type="text"
          placeholder="Mã hẹn / Mã phiếu (VD: APPT-123456, TC-2611...)"
          value={code}
          onChange={(e) => setCode(e.target.value)}
          style={{ minWidth: '240px' }}
        />
        <input
          type="tel"
          placeholder="Số điện thoại đăng ký (VD: 0901234567)"
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          style={{ minWidth: '180px' }}
        />
        <button type="submit" className="button" disabled={loading}>
          {loading ? 'Đang tra cứu...' : 'Tra cứu tiến trình'} <span>→</span>
        </button>
      </form>

      {searched && ticket && (
        <div className="tracking-result-card">
          <div className="result-header">
            <div>
              <span className="badge-pill">Mã đơn: {ticket.code}</span>
              <h3 style={{ marginTop: '8px', fontSize: '18px', fontWeight: 600 }}>
                {ticket.modelName} — {ticket.customerName}
              </h3>
              <p style={{ color: '#7b7b7b', fontSize: '12.5px', marginTop: '4px' }}>
                Số điện thoại: {ticket.phone} | Thời gian: {ticket.scheduledAt}
              </p>
            </div>
            <div className="status-tag">
              <span className="status-dot"></span>
              <strong>{steps[currentIdx]?.label || ticket.status}</strong>
            </div>
          </div>

          <div className="issue-box">
            <strong>Hiện trạng / Yêu cầu:</strong> {ticket.issue}
          </div>

          {/* Timeline */}
          <div className="timeline-track">
            {steps.map((st, i) => (
              <div key={st.key} className={`timeline-step ${i <= currentIdx ? 'active' : ''}`}>
                <div className="step-circle">{i <= currentIdx ? '✓' : i + 1}</div>
                <div className="step-title">{st.label}</div>
                <div className="step-desc">{st.desc}</div>
              </div>
            ))}
          </div>
        </div>
      )}

      {searched && !ticket && !loading && (
        <div className="no-result-card">
          <p>Không tìm thấy phiếu sửa chữa khớp với thông tin đã nhập.</p>
          <span style={{ fontSize: '12.5px', color: '#c53030', display: 'block', marginTop: '6px' }}>
            {errorMessage || 'Vui lòng kiểm tra lại chính xác Mã phiếu và Số điện thoại đã đăng ký.'}
          </span>
        </div>
      )}
    </section>
  )
}
