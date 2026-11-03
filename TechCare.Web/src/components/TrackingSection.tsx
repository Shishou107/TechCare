import React, { useState, useEffect, useCallback, useRef } from 'react'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import { API_BASE_URL } from '../api/client'

export interface ShipmentEventDto {
  occurredAtUtc: string
  status?: string
  providerStatus?: string
  internalStatus?: string
  location?: string | null
  locationText?: string | null
  description?: string | null
  note?: string | null
  latitude?: number | null
  longitude?: number | null
}

export interface ShipmentTrackingDto {
  shipmentId: number
  appointmentId?: number | null
  repairTicketId?: number | null
  trackingCode?: string
  waybillCode?: string
  provider?: string
  carrierCode?: string
  status?: string
  statusCode?: string
  statusName?: string
  shippingFee?: number
  feeAmount?: number
  printUrl?: string | null
  senderLatitude?: number | null
  senderLongitude?: number | null
  receiverLatitude?: number | null
  receiverLongitude?: number | null
  events: ShipmentEventDto[]
}

interface TicketInfo {
  code: string
  customerName: string
  phone: string
  modelName: string
  status: string
  scheduledAt: string
  issue: string
  shipment?: ShipmentTrackingDto | null
  repairTicketId?: number
  technicianName?: string
  technicianAvatarUrl?: string
  technicianSpecialty?: string
  ratingScore?: number
  ratingComment?: string
  preferredTechnicianName?: string
}

interface TrackingSectionProps {
  initialCode?: string
  initialPhone?: string
  autoSearch?: boolean
}

// Map Component with Leaflet (Real Coordinates & Dynamic Points)
function ShipmentRouteMap({ shipment, customerLocation }: { shipment: ShipmentTrackingDto; customerLocation?: string }) {
  const mapContainerRef = useRef<HTMLDivElement>(null)
  const mapInstanceRef = useRef<L.Map | null>(null)

  // Collect valid points strictly from geocoded sender, receiver, and tracked events
  const points: { coord: [number, number]; label: string; desc: string; isDone: boolean }[] = []

  if (typeof shipment.senderLatitude === 'number' && typeof shipment.senderLongitude === 'number') {
    points.push({
      coord: [shipment.senderLatitude, shipment.senderLongitude],
      label: 'Điểm gửi',
      desc: customerLocation || 'Địa chỉ bên gửi',
      isDone: true,
    })
  }

  if (shipment.events && shipment.events.length > 0) {
    shipment.events.forEach((evt, idx) => {
      const lat = evt.latitude
      const lng = evt.longitude
      if (typeof lat === 'number' && typeof lng === 'number') {
        const isDuplicate = points.some(p => Math.abs(p.coord[0] - lat) < 0.0001 && Math.abs(p.coord[1] - lng) < 0.0001)
        if (!isDuplicate) {
          points.push({
            coord: [lat, lng],
            label: `Trạm ${idx + 1}`,
            desc: evt.location || evt.locationText || evt.description || evt.note || 'Trạm trung chuyển bưu cục',
            isDone: true,
          })
        }
      }
    })
  }

  if (typeof shipment.receiverLatitude === 'number' && typeof shipment.receiverLongitude === 'number') {
    const isDuplicate = points.some(p => Math.abs(p.coord[0] - shipment.receiverLatitude!) < 0.0001 && Math.abs(p.coord[1] - shipment.receiverLongitude!) < 0.0001)
    if (!isDuplicate) {
      const isDelivered = shipment.statusCode === 'DELIVERED' || shipment.status === 'DELIVERED'
      points.push({
        coord: [shipment.receiverLatitude, shipment.receiverLongitude],
        label: 'Điểm nhận',
        desc: 'Địa chỉ bên nhận hàng',
        isDone: isDelivered,
      })
    }
  }

  useEffect(() => {
    if (!mapContainerRef.current || points.length < 2) return

    if (mapInstanceRef.current) {
      mapInstanceRef.current.remove()
      mapInstanceRef.current = null
    }

    const map = L.map(mapContainerRef.current, {
      zoomControl: true,
      attributionControl: false,
    }).setView(points[0].coord, 12)

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 18,
    }).addTo(map)

    const createIcon = (label: string, isDone: boolean) =>
      L.divIcon({
        className: 'custom-map-marker',
        html: `<div style="
          background: ${isDone ? '#111111' : '#ffffff'};
          color: ${isDone ? '#ffffff' : '#111111'};
          border: 2px solid #111111;
          border-radius: 999px;
          padding: 4px 10px;
          font-size: 11px;
          font-weight: 700;
          white-space: nowrap;
          box-shadow: 0 2px 8px rgba(0,0,0,0.18);
        ">${label}</div>`,
        iconSize: [80, 28],
        iconAnchor: [40, 14],
      })

    points.forEach((p) => {
      L.marker(p.coord, { icon: createIcon(p.label, p.isDone) })
        .addTo(map)
        .bindPopup(`<b>${p.label}:</b> ${p.desc}`)
    })

    const latLngs = points.map(p => p.coord)
    const routeLine = L.polyline(latLngs, {
      color: '#111111',
      weight: 3,
      dashArray: '6, 8',
      opacity: 0.8,
    }).addTo(map)

    map.fitBounds(routeLine.getBounds(), { padding: [40, 40] })
    mapInstanceRef.current = map

    return () => {
      if (mapInstanceRef.current) {
        mapInstanceRef.current.remove()
        mapInstanceRef.current = null
      }
    }
  }, [points.length])

  // If fewer than 2 valid coordinate points exist, hide map and display notice as specified in FIX-1
  if (points.length < 2) {
    return (
      <div className="no-map-coords-notice" style={{ marginTop: '16px', padding: '24px', textAlign: 'center', color: '#666', background: '#fafaf8', borderRadius: '10px', border: '1px dashed #d4d4d0', fontSize: '13px' }}>
        Chưa có dữ liệu vị trí cho đơn này
      </div>
    )
  }

  const carrierName = shipment.carrierCode || shipment.provider || 'GHN'

  return (
    <div style={{ marginTop: '16px', borderRadius: '10px', overflow: 'hidden', border: '1px solid var(--line)' }}>
      <div
        ref={mapContainerRef}
        style={{ width: '100%', height: '280px', background: '#f4f4f2' }}
      />
      <div style={{ padding: '8px 14px', background: '#fafaf8', borderTop: '1px solid var(--line)', fontSize: '11px', color: '#666', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <span>Bản đồ hành trình lộ trình bưu phẩm thời gian thực (Leaflet / OpenStreetMap)</span>
        <span style={{ fontWeight: 600, color: '#111' }}>Đối tác: {carrierName} Express</span>
      </div>
    </div>
  )
}

export function TrackingSection({ initialCode = '', initialPhone = '', autoSearch = false }: TrackingSectionProps) {
  const [code, setCode] = useState(initialCode)
  const [phone, setPhone] = useState(initialPhone)
  const [loading, setLoading] = useState(false)
  const [ticket, setTicket] = useState<TicketInfo | null>(null)
  const [searched, setSearched] = useState(false)
  const [ratingScoreInput, setRatingScoreInput] = useState(5)
  const [ratingCommentInput, setRatingCommentInput] = useState('')
  const [ratingSubmitting, setRatingSubmitting] = useState(false)
  const [ratingSubmitted, setRatingSubmitted] = useState(false)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)

  const executeLookup = useCallback(async (searchCode: string, searchPhone: string) => {
    if (!searchCode.trim() || !searchPhone.trim()) {
      setErrorMessage('Vui lòng nhập cả Mã phiếu/đơn và Số điện thoại để tra cứu bảo mật.')
      setSearched(true)
      return
    }

    setLoading(true)
    setSearched(true)
    setTicket(null)
    setErrorMessage(null)

    try {
      const queryParams = new URLSearchParams({
        code: searchCode.trim(),
        phone: searchPhone.trim(),
      })
      const res = await fetch(`${API_BASE_URL}/api/v1/appointments/track?${queryParams.toString()}`)
      if (res.ok) {
        const result = await res.json()
        if (result && result.found && result.data) {
          const found = result.data
          const issueText = found.customerDescription || found.subject || found.customerIssue || 'Kiểm tra phần cứng tổng thể'
          const realShipment: ShipmentTrackingDto | null = result.shipment || null

          setTicket({
            code: found.appointmentCode || found.ticketCode || searchCode.trim().toUpperCase(),
            customerName: found.customerName || 'Khách hàng',
            phone: found.customerPhone || searchPhone.trim(),
            modelName: found.modelName || 'Laptop',
            status: found.status || 'BOOKED',
            scheduledAt: found.scheduledAtUtc ? new Date(found.scheduledAtUtc).toLocaleString('vi-VN') : 'Đang xử lý',
            issue: issueText,
            shipment: realShipment,
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
  }, [])

  useEffect(() => {
    if (initialCode) setCode(initialCode)
    if (initialPhone) setPhone(initialPhone)
    if (autoSearch && initialCode && initialPhone) {
      executeLookup(initialCode, initialPhone)
    }
  }, [initialCode, initialPhone, autoSearch, executeLookup])

  const handleSubmitRating = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!ticket?.repairTicketId || !phone) return
    setRatingSubmitting(true)
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/repairs/${ticket.repairTicketId}/rating`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          phone: phone.trim(),
          score: ratingScoreInput,
          comment: ratingCommentInput.trim(),
        }),
      })
      if (res.ok) {
        setRatingSubmitted(true)
        setTicket({ ...ticket, ratingScore: ratingScoreInput, ratingComment: ratingCommentInput.trim() })
      } else {
        const err = await res.json().catch(() => null)
        alert(err?.message || 'Không thể gửi đánh giá.')
      }
    } catch {
      alert('Lỗi kết nối khi gửi đánh giá.')
    } finally {
      setRatingSubmitting(false)
    }
  }

  const handleLookup = (e: React.FormEvent) => {
    e.preventDefault()
    executeLookup(code, phone)
  }

  const standardSteps = [
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

  const mapShipmentStatusText = (st: string) => {
    switch (st?.toUpperCase()) {
      case 'CREATED': return 'Đã tạo vận đơn trên hệ thống hãng'
      case 'PICKING': return 'Đang điều phối bưu tá đến lấy hàng'
      case 'PICKED': return 'Bưu tá đã lấy hàng thành công'
      case 'IN_TRANSIT': return 'Đang vận chuyển liên tỉnh qua trung tâm khai thác'
      case 'OUT_FOR_DELIVERY': return 'Đang giao hàng tới trung tâm TechCare'
      case 'DELIVERED': return 'Đã giao thành công tới trung tâm TechCare'
      case 'DELIVERY_FAILED': return 'Giao hàng không thành công'
      case 'RETURNING': return 'Đang chuyển hoàn'
      case 'RETURNED': return 'Đã hoàn trả'
      case 'CANCELLED': return 'Đã hủy vận đơn'
      case 'EXCEPTION': return 'Bưu kiện phát sinh sự cố kiểm tra'
      default: return st || 'Đang xử lý vận chuyển'
    }
  }

  return (
    <section id="tracking" className="tracking-section section-pad">
      <div className="section-heading">
        <div>
          <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '8px' }}>
            MINH BẠCH TIẾN TRÌNH & HÀNH TRÌNH
          </div>
          <h2>Tra cứu đơn sửa & Bảo hành.</h2>
        </div>
        <p className="section-intro">
          Nhập mã phiếu hẹn hoặc mã vận đơn cùng số điện thoại đăng ký của bạn để theo dõi tiến trình trực tiếp theo thời gian thực.
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
              <strong>{standardSteps[currentIdx]?.label || ticket.status}</strong>
            </div>
          </div>

          <div className="issue-box">
            <strong>Hiện trạng / Yêu cầu tiếp nhận:</strong> {ticket.issue}
          </div>

          {/* Standard In-Shop Repair Timeline */}
          <div style={{ marginBottom: '28px' }}>
            <h4 style={{ fontSize: '13px', textTransform: 'uppercase', letterSpacing: '0.04em', color: '#555', marginBottom: '14px' }}>
              Tiến trình kỹ thuật tại xưởng TechCare:
            </h4>
            <div className="timeline-track">
              {standardSteps.map((st, i) => (
                <div key={st.key} className={`timeline-step ${i <= currentIdx ? 'active' : ''}`}>
                  <div className="step-circle">{i <= currentIdx ? '✓' : i + 1}</div>
                  <div className="step-title">{st.label}</div>
                  <div className="step-desc">{st.desc}</div>
                </div>
              ))}
            </div>
          </div>

          {/* Card Kỹ thuật viên phụ trách (Phase 5B) */}
          {ticket.technicianName && (
            <div style={{ background: '#fafafa', border: '1px solid var(--line)', borderRadius: '10px', padding: '18px 20px', marginBottom: '24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '16px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
                <img
                  src={ticket.technicianAvatarUrl || 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&q=80&w=120'}
                  alt={ticket.technicianName}
                  style={{ width: '48px', height: '48px', borderRadius: '50%', objectFit: 'cover' }}
                />
                <div>
                  <div style={{ fontSize: '11px', textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--sub)', fontWeight: 600 }}>KỸ THUẬT VIÊN PHỤ TRÁCH MÁY CỦA BẠN</div>
                  <h4 style={{ fontSize: '15px', fontWeight: 600, margin: '2px 0' }}>{ticket.technicianName}</h4>
                  <div style={{ fontSize: '12px', color: 'var(--sub)' }}>{ticket.technicianSpecialty || 'Chuyên gia phần cứng TechCare'}</div>
                </div>
              </div>

              <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                <button
                  type="button"
                  className="secondary-btn"
                  style={{ fontSize: '12.5px', padding: '7px 14px' }}
                  onClick={() => {
                    const chatBtn = document.querySelector('.chat-trigger-btn') as HTMLElement
                    if (chatBtn) chatBtn.click()
                  }}
                >
                  Nhắn hỗ trợ về máy này
                </button>
              </div>
            </div>
          )}

          {/* Rating Block for Completed / Returned Tickets (Phase 5B) */}
          {(ticket.status === 'COMPLETED' || ticket.status === 'RETURNED' || ticket.status === 'READY') && (
            <div style={{ background: '#fff', border: '1px solid var(--line)', borderRadius: '10px', padding: '20px', marginBottom: '24px' }}>
              <div className="eyebrow" style={{ textAlign: 'left', marginBottom: '4px' }}>ĐÁNH GIÁ CHẤT LƯỢNG SỬA CHỮA</div>
              {ticket.ratingScore || ratingSubmitted ? (
                <div>
                  <div style={{ fontSize: '16px', color: '#eab308', marginBottom: '6px' }}>
                    {'★'.repeat(ticket.ratingScore || ratingScoreInput)}{'☆'.repeat(5 - (ticket.ratingScore || ratingScoreInput))}
                  </div>
                  <p style={{ fontSize: '13px', color: 'var(--ink)', margin: 0 }}>
                    {ticket.ratingComment || ratingCommentInput || 'Đã gửi đánh giá.'}
                  </p>
                  <span style={{ fontSize: '11.5px', color: 'var(--sub)', marginTop: '4px', display: 'block' }}>
                    ✓ Bạn đã gửi đánh giá cho dịch vụ này. Cảm ơn phản hồi quý báu của bạn!
                  </span>
                </div>
              ) : (
                <form onSubmit={handleSubmitRating}>
                  <p style={{ fontSize: '13px', color: 'var(--sub)', marginBottom: '10px' }}>
                    Thiết bị của bạn đã được kiểm chuẩn hoàn tất. Hãy cho TechCare biết mức độ hài lòng của bạn:
                  </p>
                  <div style={{ display: 'flex', gap: '8px', fontSize: '24px', cursor: 'pointer', marginBottom: '12px' }}>
                    {[1, 2, 3, 4, 5].map((s) => (
                      <span
                        key={s}
                        onClick={() => setRatingScoreInput(s)}
                        style={{ color: s <= ratingScoreInput ? '#eab308' : '#d1d5db', transition: 'color 0.15s' }}
                      >
                        ★
                      </span>
                    ))}
                    <span style={{ fontSize: '13px', color: 'var(--sub)', alignSelf: 'center', marginLeft: '6px' }}>
                      ({ratingScoreInput} / 5 sao)
                    </span>
                  </div>

                  <textarea
                    placeholder="Nhận xét của bạn về tay nghề kỹ thuật và sự nhiệt tình của nhân viên..."
                    value={ratingCommentInput}
                    onChange={(e) => setRatingCommentInput(e.target.value)}
                    rows={2}
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--line)', fontSize: '13px', marginBottom: '10px' }}
                  />

                  <button type="submit" className="button" style={{ padding: '8px 18px', fontSize: '13px' }} disabled={ratingSubmitting}>
                    {ratingSubmitting ? 'Đang gửi...' : 'Gửi đánh giá dịch vụ'} <span>→</span>
                  </button>
                </form>
              )}
            </div>
          )}

          {/* Real Courier Transport Route & Events (GHN Integration) */}
          {ticket.shipment ? (
            <div className="courier-timeline-section" style={{ marginTop: '24px', paddingTop: '20px', borderTop: '1px solid var(--line)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px', flexWrap: 'wrap', gap: '8px' }}>
                <div>
                  <h4 style={{ fontSize: '14px', fontWeight: 600, color: 'var(--ink)' }}>
                    Hành trình vận chuyển thực tế — {ticket.shipment.carrierCode || ticket.shipment.provider || 'GHN'} Express
                  </h4>
                  <span style={{ fontSize: '12px', color: '#555' }}>
                    Mã vận đơn: <strong>{ticket.shipment.waybillCode || ticket.shipment.trackingCode}</strong> | Trạng thái: <strong>{mapShipmentStatusText(ticket.shipment.status || ticket.shipment.statusCode || '')}</strong>
                    {((ticket.shipment.shippingFee ?? ticket.shipment.feeAmount ?? 0) > 0) && ` | Cước vận chuyển: ${(ticket.shipment.shippingFee ?? ticket.shipment.feeAmount ?? 0).toLocaleString('vi-VN')}đ`}
                  </span>
                </div>
                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <span style={{ fontSize: '11px', background: '#111111', color: '#ffffff', padding: '4px 10px', borderRadius: '4px', fontWeight: 600 }}>
                    Bảo hiểm kiện hàng toàn phần
                  </span>
                  {ticket.shipment.printUrl && (
                    <a
                      href={ticket.shipment.printUrl}
                      target="_blank"
                      rel="noreferrer"
                      style={{ fontSize: '11px', border: '1px solid #111111', padding: '3px 8px', borderRadius: '4px', color: '#111111' }}
                    >
                      In vận đơn
                    </a>
                  )}
                </div>
              </div>

              {/* Leaflet Route Map */}
              <ShipmentRouteMap shipment={ticket.shipment} customerLocation={ticket.customerName} />

              {/* Chronological Real Events Timeline */}
              <div style={{ marginTop: '20px' }}>
                <h5 style={{ fontSize: '12.5px', fontWeight: 600, color: '#333', marginBottom: '10px', textTransform: 'uppercase', letterSpacing: '0.03em' }}>
                  Chi tiết sự kiện quét mã vận đơn (Thời gian thực):
                </h5>
                {ticket.shipment.events && ticket.shipment.events.length > 0 ? (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                    {ticket.shipment.events.map((ev, idx) => (
                      <div
                        key={idx}
                        style={{
                          display: 'grid',
                          gridTemplateColumns: '130px 1fr',
                          gap: '12px',
                          padding: '10px 14px',
                          background: idx === 0 ? '#ffffff' : '#fafaf8',
                          border: '1px solid ' + (idx === 0 ? '#111111' : '#e4e4e0'),
                          borderRadius: '8px',
                          fontSize: '12px',
                        }}
                      >
                        <div style={{ color: '#666', fontWeight: 500 }}>
                          {new Date(ev.occurredAtUtc).toLocaleString('vi-VN')}
                        </div>
                        <div>
                          <strong style={{ color: '#111' }}>{mapShipmentStatusText(ev.status || ev.providerStatus || ev.internalStatus || '')}</strong>
                          {ev.location && <span style={{ color: '#555', marginLeft: '6px' }}>— {ev.location}</span>}
                          {ev.note && <div style={{ color: '#777', fontSize: '11.5px', marginTop: '2px' }}>{ev.note}</div>}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div style={{ padding: '14px', background: '#fafaf8', border: '1px solid #e4e4e0', borderRadius: '8px', fontSize: '12px', color: '#666' }}>
                    Vận đơn đã khởi tạo trên hệ thống {ticket.shipment.provider} Express. Bưu tá đang trên đường đến lấy thiết bị và cập nhật sự kiện quét mã đầu tiên.
                  </div>
                )}
              </div>
            </div>
          ) : null}
        </div>
      )}

      {searched && !ticket && !loading && (
        <div className="no-result-card">
          <p>Không tìm thấy phiếu sửa chữa hoặc đơn hẹn khớp với thông tin đã nhập.</p>
          <span style={{ fontSize: '12.5px', color: '#c53030', display: 'block', marginTop: '6px' }}>
            {errorMessage || 'Vui lòng kiểm tra lại chính xác Mã phiếu và Số điện thoại đã đăng ký.'}
          </span>
        </div>
      )}
    </section>
  )
}
