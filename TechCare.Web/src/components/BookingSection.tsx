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

interface BookingSectionProps {
  selectedIssue?: string
}

export function BookingSection({ selectedIssue = '' }: BookingSectionProps) {
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
    issueDescription: selectedIssue,
    serviceType: 'AT_STORE', // AT_STORE or PICKUP
  })

  const [submitting, setSubmitting] = useState(false)
  const [submitResult, setSubmitResult] = useState<{ success: boolean; code?: string; msg?: string } | null>(null)

  // Sync issue description when selected from symptoms or services
  useEffect(() => {
    if (selectedIssue) {
      setFormData((prev) => ({
        ...prev,
        issueDescription: selectedIssue,
      }))
    }
  }, [selectedIssue])

  // Polling listener for 1-Click quick-scan
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
          setTimeout(() => setAutoFilled(false), 3500)
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
    a.download = `TechCare-Scan-${sessionCode}.bat`
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
          msg: 'Yêu cầu kiểm tra máy đã được tiếp nhận thành công vào hệ thống TechCare!',
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
        msg: 'Không thể kết nối đến máy chủ API (localhost:5000). Vui lòng kiểm tra dịch vụ backend.',
      })
    } finally {
      setSubmitting(false)
    }
  }

  const handleReset = () => {
    setSubmitResult(null)
    setFormData({
      customerName: '',
      customerPhone: '',
      customerEmail: '',
      modelName: '',
      serialNumber: '',
      cpu: '',
      ram: '',
      storage: '',
      operatingSystem: '',
      issueDescription: '',
      serviceType: 'AT_STORE',
    })
  }

  return (
    <section id="booking-section" className="booking-section section-pad">
      <div className="booking-container-photo1">
        {/* Left Column: Exactly matching Photo 1 */}
        <div className="booking-left-copy">
          <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '8px' }}>
            TIẾP NHẬN YÊU CẦU TRỰC TUYẾN
          </div>
          <h2>Đặt lịch kiểm tra máy ngay.</h2>
          <p className="booking-lead-text">
            Để lại thông tin hoặc sử dụng tính năng Quét cấu hình máy 1-click bên dưới. Cấu hình phần cứng (Model, CPU, RAM, Ổ cứng) sẽ được tự động điền vào đơn sửa chữa.
          </p>

          <div className="booking-online-badge">
            <span className="dot"></span>
            <span>Hệ thống trực tuyến hoạt động 24/7 | Chi nhánh TechCare</span>
          </div>
        </div>

        {/* Right Column: Form matching Photo 1 */}
        <div className="booking-right-form">
          {submitResult?.success ? (
            <div className="modal-success-box" style={{ background: '#fbfbf9', border: '1px solid #e4e4e0', padding: '36px', borderRadius: '14px', textAlign: 'center' }}>
              <div className="success-icon" style={{ width: '48px', height: '48px', borderRadius: '50%', background: 'var(--pcb)', color: '#064e3b', display: 'grid', placeItems: 'center', margin: '0 auto 16px', fontSize: '20px', fontWeight: 'bold' }}>
                ✓
              </div>
              <h3 style={{ fontSize: '20px', fontWeight: 600, marginBottom: '8px' }}>ĐẶT LỊCH THÀNH CÔNG!</h3>
              <p style={{ fontSize: '15px', color: '#111', marginBottom: '12px' }}>
                Mã phiếu hẹn: <strong>{submitResult.code}</strong>
              </p>
              <p style={{ fontSize: '13px', color: '#666', lineHeight: 1.6, maxWidth: '480px', margin: '0 auto 24px' }}>
                Kỹ thuật viên của TechCare sẽ liên hệ lại qua số <strong>{formData.customerPhone}</strong> trong vòng 15 phút để xác nhận và chuẩn bị linh kiện.
              </p>
              <button
                type="button"
                className="button"
                onClick={handleReset}
              >
                Đặt lịch thiết bị khác <span>→</span>
              </button>
            </div>
          ) : (
            <form className="booking-form" onSubmit={handleSubmit}>
              {/* Quick-Scan Banner (NO lightning emoji, NO clipboard button) */}
              <div className="scan-banner">
                <div className="scan-banner-info">
                  <h4>Quét cấu hình máy 1-Click</h4>
                  <p>
                    {isListening
                      ? `Đang kết nối (Mã phiên: ${sessionCode}). Chạy file .bat vừa tải để thông số tự điền...`
                      : autoFilled
                      ? '✓ Đã nhận diện thành công cấu hình máy!'
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

              {/* Service Type (NO emojis) */}
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

              {/* Row 1: Họ và tên | Số điện thoại */}
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

              {/* Row 2: Email | Model máy */}
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

              {/* Row 3: CPU | RAM */}
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

              {/* Row 4: SSD | OS */}
              <div className="form-row-2">
                <div>
                  <label>Ổ cứng (SSD / HDD)</label>
                  <input
                    type="text"
                    placeholder="Ví dụ: 512GB NVMe SSD"
                    className={autoFilled ? 'auto-filled-highlight' : ''}
                    value={formData.storage}
                    onChange={(e) => setFormData({ ...formData, storage: e.target.value })}
                  />
                </div>
                <div>
                  <label>Hệ điều hành</label>
                  <input
                    type="text"
                    placeholder="Ví dụ: Windows 11 Pro"
                    className={autoFilled ? 'auto-filled-highlight' : ''}
                    value={formData.operatingSystem}
                    onChange={(e) => setFormData({ ...formData, operatingSystem: e.target.value })}
                  />
                </div>
              </div>

              {/* Row 5: Issue Description */}
              <div>
                <label>Tình trạng lỗi cần sửa chữa *</label>
                <textarea
                  rows={3}
                  required
                  placeholder="Mô tả hiện tượng: bật không lên nguồn, màn hình sọc, quạt kêu to, v.v."
                  value={formData.issueDescription}
                  onChange={(e) => setFormData({ ...formData, issueDescription: e.target.value })}
                />
              </div>

              {submitResult && !submitResult.success && (
                <div style={{ color: '#dc2626', fontSize: '12.5px', marginBottom: '14px', background: '#fee2e2', padding: '10px 14px', borderRadius: '8px' }}>
                  {submitResult.msg}
                </div>
              )}

              {/* Submit Button (Photo 1 exact recreation, sleek pill button with circuit glow) */}
              <div>
                <button
                  type="submit"
                  className="booking-submit-pill"
                  disabled={submitting}
                >
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
    </section>
  )
}

