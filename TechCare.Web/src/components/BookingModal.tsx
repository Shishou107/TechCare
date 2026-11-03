import React, { useState, useEffect } from 'react'

interface HardwareSpecs {
  sessionCode?: string
  manufacturer: string
  model: string
  serialNumber: string
  cpu: string
  ram: string
  storage: string
  operatingSystem: string
}

interface BookingModalProps {
  isOpen: boolean
  onClose: () => void
  initialIssue?: string
}

export function BookingModal({ isOpen, onClose, initialIssue = '' }: BookingModalProps) {
  const [sessionCode] = useState(() => 'FL-' + Math.floor(1000 + Math.random() * 9000))
  const [isListening, setIsListening] = useState(false)
  const [autoFilled, setAutoFilled] = useState(false)

  const [formData, setFormData] = useState({
    customerName: '',
    customerPhone: '',
    customerEmail: '',
    modelName: '',
    serialNumber: '',
    cpu: '',
    ram: '',
    storage: '',
    operatingSystem: '',
    issueDescription: initialIssue,
    serviceType: 'AT_STORE', // AT_STORE or PICKUP
  })

  const [submitting, setSubmitting] = useState(false)
  const [submitResult, setSubmitResult] = useState<{ success: boolean; code?: string; msg?: string } | null>(null)

  // Polling listener for quick-scan
  useEffect(() => {
    if (!isListening || !sessionCode) return

    const interval = setInterval(async () => {
      try {
        const res = await fetch(`http://localhost:5000/api/v1/diagnostics/quick-sync/${sessionCode}`)
        const data = await res.json()
        if (data && data.synced && data.data) {
          const spec: HardwareSpecs = data.data
          setFormData((prev) => ({
            ...prev,
            modelName: spec.model || prev.modelName,
            serialNumber: spec.serialNumber || prev.serialNumber,
            cpu: spec.cpu || prev.cpu,
            ram: spec.ram || prev.ram,
            storage: spec.storage || prev.storage,
            operatingSystem: spec.operatingSystem || prev.operatingSystem,
          }))

          setIsListening(false)
          setAutoFilled(true)
          setTimeout(() => setAutoFilled(false), 3000)
        }
      } catch (err) {
        console.warn('Sync polling waiting...', err)
      }
    }, 1500)

    return () => clearInterval(interval)
  }, [isListening, sessionCode])

  const handleStartScan = () => {
    setIsListening(true)
    const downloadUrl = `http://localhost:5000/api/v1/diagnostics/download-script/${sessionCode}`
    const a = document.createElement('a')
    a.href = downloadUrl
    a.download = `FixLab-Scan-${sessionCode}.bat`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    setSubmitResult(null)

    try {
      const payload = {
        customerName: formData.customerName.trim(),
        customerPhone: formData.customerPhone.trim(),
        customerEmail: formData.customerEmail?.trim() || null,
        modelName: formData.modelName?.trim() || null,
        serialNumber: formData.serialNumber?.trim() || null,
        cpu: formData.cpu?.trim() || null,
        ram: formData.ram?.trim() || null,
        storage: formData.storage?.trim() || null,
        operatingSystem: formData.operatingSystem?.trim() || null,
        issueDescription: `[${formData.serviceType === 'PICKUP' ? 'GIAO NHẬN TẬN NƠI' : 'TẠI TIỆM'}] ${formData.issueDescription.trim()}`,
      }

      const res = await fetch('http://localhost:5000/api/v1/appointments/submit', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json; charset=utf-8' },
        body: JSON.stringify(payload),
      })

      const data = await res.json()
      if (res.ok && data.success) {
        setSubmitResult({
          success: true,
          code: data.appointmentCode,
          msg: 'Yêu cầu sửa máy đã được tiếp nhận thành công vào hệ thống FixLab!',
        })
      } else {
        setSubmitResult({
          success: false,
          msg: data.message || 'Lỗi kiểm tra dữ liệu hoặc máy chủ.',
        })
      }
    } catch {
      setSubmitResult({
        success: false,
        msg: 'Không thể kết nối đến máy chủ API (localhost:5000). Vui lòng thử lại sau.',
      })
    } finally {
      setSubmitting(false)
    }
  }

  if (!isOpen) return null

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div>
            <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '4px' }}>
              TIẾP NHẬN SỬA CHỮA
            </div>
            <h3>Đặt lịch kiểm tra máy</h3>
          </div>
          <button type="button" className="close-btn" onClick={onClose}>
            ✕
          </button>
        </div>

        {submitResult?.success ? (
          <div className="modal-success-box" style={{ textAlign: 'center', padding: '24px 16px' }}>
            <div style={{ width: '44px', height: '44px', borderRadius: '50%', background: 'var(--pcb)', color: '#064e3b', display: 'grid', placeItems: 'center', margin: '0 auto 12px', fontSize: '18px', fontWeight: 'bold' }}>✓</div>
            <h4>ĐẶT LỊCH THÀNH CÔNG!</h4>
            <p className="code-display" style={{ margin: '10px 0' }}>Mã phiếu hẹn: <strong>{submitResult.code}</strong></p>
            <p className="desc" style={{ color: '#666', fontSize: '12.5px', lineHeight: 1.5 }}>
              Kỹ thuật viên của FixLab sẽ liên hệ lại qua số <strong>{formData.customerPhone}</strong> trong vòng 15 phút để xác nhận và chuẩn bị linh kiện.
            </p>
            <div style={{ marginTop: '20px' }}>
              <button type="button" className="button" onClick={onClose}>
                Đóng thông báo <span>→</span>
              </button>
            </div>
          </div>
        ) : (
          <form className="booking-form" onSubmit={handleSubmit}>
            {/* Quick-Scan Banner (NO lightning emoji, NO clipboard button) */}
            <div className="scan-banner">
              <div className="scan-banner-info">
                <h4>Quét cấu hình máy 1-Click</h4>
                <p>
                  {isListening
                    ? `Đang lắng nghe cấu hình (Mã: ${sessionCode})...`
                    : autoFilled
                    ? '✓ Đã tự động nhận diện cấu hình máy!'
                    : 'Tải script quét để tự động điền CPU, RAM, Model, SSD'}
                </p>
              </div>
              <div>
                <button
                  type="button"
                  className={`scan-btn ${isListening ? 'scanning' : ''}`}
                  onClick={handleStartScan}
                >
                  {isListening ? 'Đang đợi đồng bộ...' : 'Quét tự động (.bat)'}
                </button>
              </div>
            </div>

            {/* Service Option (NO emojis) */}
            <div className="service-choice-row">
              <label
                className={`choice-pill ${formData.serviceType === 'AT_STORE' ? 'active' : ''}`}
                onClick={() => setFormData({ ...formData, serviceType: 'AT_STORE' })}
              >
                <span>Mang tới tiệm (Kiểm tra lấy liền)</span>
              </label>
              <label
                className={`choice-pill ${formData.serviceType === 'PICKUP' ? 'active' : ''}`}
                onClick={() => setFormData({ ...formData, serviceType: 'PICKUP' })}
              >
                <span>Nhận máy tận nơi (Nội thành)</span>
              </label>
            </div>

            {/* Customer Inputs */}
            <div className="form-row-2">
              <div>
                <label>Họ và tên *</label>
                <input
                  type="text"
                  required
                  placeholder="Ví dụ: Nguyễn Văn An"
                  value={formData.customerName}
                  onChange={(e) => setFormData({ ...formData, customerName: e.target.value })}
                />
              </div>
              <div>
                <label>Số điện thoại *</label>
                <input
                  type="tel"
                  required
                  placeholder="Ví dụ: 0912345678"
                  value={formData.customerPhone}
                  onChange={(e) => setFormData({ ...formData, customerPhone: e.target.value })}
                />
              </div>
            </div>

            <div className="form-row-2">
              <div>
                <label>Email liên hệ</label>
                <input
                  type="email"
                  placeholder="Ví dụ: an.nguyen@email.com"
                  value={formData.customerEmail}
                  onChange={(e) => setFormData({ ...formData, customerEmail: e.target.value })}
                />
              </div>
              <div>
                <label>Model máy (Tự động hoặc nhập tay)</label>
                <input
                  type="text"
                  placeholder="Ví dụ: ThinkPad X1 Carbon"
                  className={autoFilled ? 'auto-filled-highlight' : ''}
                  value={formData.modelName}
                  onChange={(e) => setFormData({ ...formData, modelName: e.target.value })}
                />
              </div>
            </div>

            <div className="form-row-2">
              <div>
                <label>Vi xử lý (CPU)</label>
                <input
                  type="text"
                  placeholder="Ví dụ: Intel Core i7-13700H"
                  className={autoFilled ? 'auto-filled-highlight' : ''}
                  value={formData.cpu}
                  onChange={(e) => setFormData({ ...formData, cpu: e.target.value })}
                />
              </div>
              <div>
                <label>Bộ nhớ (RAM)</label>
                <input
                  type="text"
                  placeholder="Ví dụ: 16 GB DDR5"
                  className={autoFilled ? 'auto-filled-highlight' : ''}
                  value={formData.ram}
                  onChange={(e) => setFormData({ ...formData, ram: e.target.value })}
                />
              </div>
            </div>

            {/* Issue Description */}
            <div>
              <label>Tình trạng lỗi cần xử lý *</label>
              <textarea
                rows={3}
                required
                placeholder="Ví dụ: Không lên nguồn, màn hình sọc, sạc không vào, máy nóng tắt đột ngột..."
                value={formData.issueDescription}
                onChange={(e) => setFormData({ ...formData, issueDescription: e.target.value })}
              />
            </div>

            {submitResult && !submitResult.success && (
              <div className="error-note" style={{ color: '#dc2626', background: '#fee2e2', padding: '10px', borderRadius: '6px', fontSize: '12px', marginBottom: '12px' }}>
                {submitResult.msg}
              </div>
            )}

            <div>
              <button type="submit" className="booking-submit-pill" disabled={submitting}>
                {submitting ? 'Đang gửi yêu cầu...' : 'Gửi yêu cầu kiểm tra máy'} <span>→</span>
              </button>
            </div>

            <p className="booking-disclaimer">
              Yêu cầu sẽ được gửi trực tiếp đến hệ thống tiếp nhận TechCare. Kỹ thuật viên sẽ liên hệ xác nhận trong vòng 15 phút.
            </p>
          </form>
        )}
      </div>
    </div>
  )
}
