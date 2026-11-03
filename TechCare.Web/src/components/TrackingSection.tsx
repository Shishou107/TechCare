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
  const [keyword, setKeyword] = useState('')
  const [loading, setLoading] = useState(false)
  const [ticket, setTicket] = useState<TicketInfo | null>(null)
  const [searched, setSearched] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  const handleLookup = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!keyword.trim()) return

    setLoading(true)
    setSearched(true)
    setTicket(null)
    setErrorMessage(null)

    try {
      // Safe, dedicated tracking endpoint (no data leaks of other customers)
      const res = await fetch(`${API_BASE_URL}/api/v1/appointments/track?keyword=${encodeURIComponent(keyword.trim())}`)
      if (res.ok) {
        const result = await res.json()
        if (result && result.found && result.data) {
          const found = result.data
          setTicket({
            code: found.appointmentCode || found.ticketCode || keyword.toUpperCase(),
            customerName: found.customerName || 'Khách hàng',
            phone: found.customerPhone || '',
            modelName: found.modelName || 'Laptop',
            status: found.status || 'BOOKED',
            scheduledAt: found.scheduledAtUtc ? new Date(found.scheduledAtUtc).toLocaleString('vi-VN') : 'Đang xử lý',
            issue: found.customerDescription || found.subject || found.customerIssue || 'Kiểm tra phần cứng tổng thể',
          })
        } else {
          setErrorMessage(result.message || 'Không tìm thấy thông tin đơn hẹn hoặc phiếu sửa chữa.')
        }
      } else {
        setErrorMessage('Không thể tra cứu vào lúc này. Vui lòng thử lại sau.')
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
          Nhập mã phiếu hẹn hoặc số điện thoại của bạn để theo dõi trực tiếp tình trạng máy theo thời gian thực.
        </p>
      </div>

      <form className="tracking-search-bar" onSubmit={handleLookup}>
        <input
          type="text"
          placeholder="Nhập mã phiếu hẹn (VD: APPT-984942, TC-2611...) hoặc số điện thoại"
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
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
          <p>Không tìm thấy phiếu sửa chữa khớp với từ khóa <strong>"{keyword}"</strong>.</p>
          <span style={{ fontSize: '12px', color: '#777', display: 'block', marginTop: '6px' }}>
            {errorMessage || 'Vui lòng kiểm tra lại số điện thoại hoặc mã phiếu được cấp khi gửi yêu cầu.'}
          </span>
        </div>
      )}
    </section>
  )
}
