import React, { useState, useEffect } from 'react'
import { API_BASE_URL } from '../api/client'

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
  // B6: Generate high-entropy cryptographic session code (16 hex chars / 64-bit entropy)
  const [sessionCode] = useState(() => {
    if (typeof crypto !== 'undefined' && crypto.getRandomValues) {
      const bytes = new Uint8Array(8)
      crypto.getRandomValues(bytes)
      return 'TC-' + Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('').toUpperCase()
    }
    return 'TC-' + Math.random().toString(36).slice(2, 14).toUpperCase()
  })

  const [isListening, setIsListening] = useState(false)
  const [autoFilled, setAutoFilled] = useState(false)
  const [showManualSpecs, setShowManualSpecs] = useState(false)

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

  const [errors, setErrors] = useState<{ [key: string]: string }>({})
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
        const res = await fetch(`${API_BASE_URL}/api/v1/diagnostics/quick-sync/${sessionCode}`)
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
          setShowManualSpecs(true) // Automatically reveal populated specs
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
    const downloadUrl = `${API_BASE_URL}/api/v1/diagnostics/download-script/${sessionCode}`
    const a = document.createElement('a')
    a.href = downloadUrl
    a.download = `TechCare-Scan-${sessionCode}.bat`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
  }

  const validateForm = () => {
    const errs: { [key: string]: string } = {}
    if (!formData.customerName.trim()) {
      errs.customerName = 'Vui lòng nhập họ và tên của bạn.'
    }
    const phone = formData.customerPhone.trim()
    if (!phone) {
      errs.customerPhone = 'Vui lòng cung cấp số điện thoại liên hệ.'
    } else if (!/^0\d{9,10}$/.test(phone)) {
      errs.customerPhone = 'Số điện thoại không đúng định dạng (VD: 0912345678).'
    }
    if (!formData.issueDescription.trim() || formData.issueDescription.trim().length < 5) {
      errs.issueDescription = 'Vui lòng mô tả hiện tượng hư hỏng tối thiểu 5 ký tự.'
    }
    setErrors(errs)
    return Object.keys(errs).length === 0
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!validateForm()) return

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

      const res = await fetch(`${API_BASE_URL}/api/v1/appointments/submit`, {
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
        const errorDetails = data.errors ? Object.values(data.errors).flat().join(' ') : null
        setSubmitResult({
          success: false,
          msg: errorDetails || data.message || 'Lỗi kiểm tra dữ liệu hoặc máy chủ.',
        })
      }
    } catch {
      setSubmitResult({
        success: false,
        msg: 'Không thể kết nối đến máy chủ API TechCare. Vui lòng kiểm tra lại dịch vụ backend.',
      })
    } finally {
      setSubmitting(false)
    }
  }

  const handleReset = () => {
    setSubmitResult(null)
    setErrors({})
    setShowManualSpecs(false)
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
        {/* Left Column: Heading and info */}
        <div className="booking-left-copy">
          <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '8px' }}>
            TIẾP NHẬN YÊU CẦU TRỰC TUYẾN
          </div>
          <h2>Đặt lịch kiểm tra máy ngay.</h2>
          <p className="booking-lead-text">
            Chỉ cần 30 giây để gửi thông tin. Kỹ thuật viên TechCare sẽ liên hệ xác nhận, chuẩn bị sẵn linh kiện và hỗ trợ kiểm tra trực tiếp tại tiệm hoặc nhận máy tận nơi.
          </p>

          <div className="booking-online-badge">
            <span className="dot"></span>
            <span>Hệ thống trực tuyến tiếp nhận 24/7 | Chi nhánh TechCare</span>
          </div>

          <div style={{ marginTop: '28px', padding: '16px 20px', background: '#ffffff', border: '1px solid var(--line)', borderRadius: '12px' }}>
            <h4 style={{ fontSize: '13.5px', fontWeight: 600, color: 'var(--ink)', marginBottom: '6px' }}>
              Quy trình tiếp nhận chuẩn hóa
            </h4>
            <p style={{ fontSize: '12.5px', color: 'var(--sub)', lineHeight: 1.6 }}>
              1. Gửi thông tin lỗi máy → 2. Nhận máy & lập biên bản ảnh chụp → 3. Đo đạc báo giá minh bạch → 4. Khách đồng ý mới sửa chữa.
            </p>
          </div>
        </div>

        {/* Right Column: 2-Step Form */}
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
            <form className="booking-form" onSubmit={handleSubmit} noValidate>
              {/* STEP 1: MANDATORY CONTACT & ISSUE DETAILS */}
              <div className="booking-step-group">
                <div className="step-badge-row">
                  <span className="step-badge">Bước 1</span>
                  <span className="step-title">Thông tin tiếp nhận (Bắt buộc)</span>
                </div>

                {/* Service Type Selection */}
                <div className="service-choice-row" style={{ marginTop: '12px' }}>
                  <label
                    className={`choice-pill ${formData.serviceType === 'AT_STORE' ? 'active' : ''}`}
                    onClick={() => setFormData({ ...formData, serviceType: 'AT_STORE' })}
                  >
                    <strong>Mang tới tiệm</strong>
                    <span className="choice-sublabel">Kiểm tra trực tiếp, lấy liền tại xưởng</span>
                  </label>
                  <label
                    className={`choice-pill ${formData.serviceType === 'PICKUP' ? 'active' : ''}`}
                    onClick={() => setFormData({ ...formData, serviceType: 'PICKUP' })}
                  >
                    <strong>Nhận máy tận nơi</strong>
                    <span className="choice-sublabel">Kỹ thuật viên nhận máy nội thành (1–2h)</span>
                  </label>
                </div>

                {/* Name & Phone */}
                <div className="form-row-2">
                  <div>
                    <label>Họ và tên *</label>
                    <input
                      type="text"
                      placeholder="Ví dụ: Nguyễn Văn An"
                      value={formData.customerName}
                      onChange={(e) => {
                        setFormData({ ...formData, customerName: e.target.value })
                        if (errors.customerName) setErrors({ ...errors, customerName: '' })
                      }}
                      className={errors.customerName ? 'input-error' : ''}
                    />
                    {errors.customerName && <span className="field-error-msg">{errors.customerName}</span>}
                  </div>
                  <div>
                    <label>Số điện thoại *</label>
                    <input
                      type="tel"
                      placeholder="Ví dụ: 0912345678"
                      value={formData.customerPhone}
                      onChange={(e) => {
                        setFormData({ ...formData, customerPhone: e.target.value })
                        if (errors.customerPhone) setErrors({ ...errors, customerPhone: '' })
                      }}
                      className={errors.customerPhone ? 'input-error' : ''}
                    />
                    {errors.customerPhone && <span className="field-error-msg">{errors.customerPhone}</span>}
                  </div>
                </div>

                {/* Issue Description */}
                <div>
                  <label>Tình trạng lỗi cần sửa chữa *</label>
                  <textarea
                    rows={3}
                    placeholder="Mô tả hiện tượng: bật không lên nguồn, màn hình sọc, quạt kêu to, đổ nước..."
                    value={formData.issueDescription}
                    onChange={(e) => {
                      setFormData({ ...formData, issueDescription: e.target.value })
                      if (errors.issueDescription) setErrors({ ...errors, issueDescription: '' })
                    }}
                    className={errors.issueDescription ? 'input-error' : ''}
                  />
                  {errors.issueDescription && <span className="field-error-msg">{errors.issueDescription}</span>}
                </div>

                {submitResult && !submitResult.success && (
                  <div style={{ color: '#dc2626', fontSize: '12.5px', marginBottom: '14px', background: '#fee2e2', padding: '10px 14px', borderRadius: '8px' }}>
                    {submitResult.msg}
                  </div>
                )}

                {/* SUBMIT BUTTON - PLACED DIRECTLY UNDER STEP 1 */}
                <div style={{ marginTop: '6px' }}>
                  <button
                    type="submit"
                    className="booking-submit-pill"
                    disabled={submitting}
                  >
                    {submitting ? 'Đang gửi yêu cầu...' : 'Gửi yêu cầu kiểm tra máy'} <span>→</span>
                  </button>
                </div>

                <p className="booking-disclaimer" style={{ marginTop: '10px' }}>
                  Yêu cầu sẽ được gửi trực tiếp đến hệ thống TechCare. Kỹ thuật viên sẽ liên hệ xác nhận trong vòng 15 phút.
                </p>
              </div>

              {/* STEP 2: HARDWARE SPECIFICATIONS (OPTIONAL & COLLAPSIBLE) */}
              <div className="booking-step-group step-2-container" style={{ marginTop: '24px' }}>
                <div className="step-badge-row">
                  <span className="step-badge step-badge-secondary">Bước 2</span>
                  <span className="step-title">Cấu hình máy (Tùy chọn — Tự động điền)</span>
                </div>

                {/* Prominent Quick-Scan Feature Card */}
                <div className="quick-scan-card-photo">
                  <div className="scan-card-body">
                    <div className="scan-icon-col">
                      <div className="scan-icon-pill">⚡ 1-Click</div>
                    </div>
                    <div className="scan-content-col">
                      <h4 style={{ fontSize: '13.5px', fontWeight: 600, color: 'var(--ink)' }}>
                        Quét tự động cấu hình máy (.bat)
                      </h4>
                      <p style={{ fontSize: '12px', color: 'var(--sub)', lineHeight: 1.5, marginTop: '2px' }}>
                        {isListening ? (
                          <span style={{ color: '#047857', fontWeight: 500 }}>
                            ● Đang đợi đồng bộ từ máy của bạn... Vui lòng mở và chạy file .bat vừa tải về.
                          </span>
                        ) : autoFilled || formData.modelName ? (
                          <span style={{ color: '#047857', fontWeight: 500 }}>
                            ✓ Đã nhận cấu hình: {formData.modelName || 'Laptop'} {formData.cpu ? `· ${formData.cpu}` : ''} {formData.ram ? `· ${formData.ram}` : ''}
                          </span>
                        ) : (
                          'Tải script nhẹ và an toàn để máy tự trích xuất Model, CPU, RAM, SSD điền vào đơn.'
                        )}
                      </p>
                    </div>
                    <div className="scan-action-col">
                      <button
                        type="button"
                        className={`scan-btn-elevated ${isListening ? 'listening' : ''}`}
                        onClick={handleStartScan}
                      >
                        {isListening ? 'Đang chờ đồng bộ...' : 'Tải & Quét tự động'}
                      </button>
                    </div>
                  </div>

                  <div className="scan-card-footer">
                    <button
                      type="button"
                      className="toggle-manual-link"
                      onClick={() => setShowManualSpecs(!showManualSpecs)}
                    >
                      {showManualSpecs ? '▲ Thu gọn thông số kỹ thuật' : '▼ Hoặc điền tay thông số nếu không thể quét'}
                    </button>
                  </div>
                </div>

                {/* Collapsible Manual Specs Input Fields */}
                {showManualSpecs && (
                  <div className="manual-specs-drawer" style={{ marginTop: '16px' }}>
                    <div className="form-row-2">
                      <div>
                        <label>Email liên hệ (Tùy chọn)</label>
                        <input
                          type="email"
                          placeholder="Ví dụ: an.nguyen@email.com"
                          value={formData.customerEmail}
                          onChange={(e) => setFormData({ ...formData, customerEmail: e.target.value })}
                        />
                      </div>
                      <div>
                        <label>Model máy</label>
                        <input
                          type="text"
                          placeholder="Ví dụ: ThinkPad X1 Carbon Gen 9"
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

                    <div className="form-row-2">
                      <div>
                        <label>Ổ cứng (SSD / HDD)</label>
                        <input
                          type="text"
                          placeholder="Ví dụ: 512 GB NVMe SSD"
                          className={autoFilled ? 'auto-filled-highlight' : ''}
                          value={formData.storage}
                          onChange={(e) => setFormData({ ...formData, storage: e.target.value })}
                        />
                      </div>
                      <div>
                        <label>Hệ điều hành</label>
                        <input
                          type="text"
                          placeholder="Ví dụ: Windows 11 Pro 64-bit"
                          className={autoFilled ? 'auto-filled-highlight' : ''}
                          value={formData.operatingSystem}
                          onChange={(e) => setFormData({ ...formData, operatingSystem: e.target.value })}
                        />
                      </div>
                    </div>
                  </div>
                )}
              </div>
            </form>
          )}
        </div>
      </div>
    </section>
  )
}
