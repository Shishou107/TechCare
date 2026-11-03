import React, { useState, useEffect, useRef } from 'react'
import { API_BASE_URL } from '../../api/client'

interface SessionItem {
  sessionId: string
  guestPhone?: string | null
  status: string // "BOT", "HUMAN", "CLOSED"
  assignedSupportId?: number | null
  lastMessage?: string | null
  startedAtUtc: string
  closedAtUtc?: string | null
}

interface MessageItem {
  messageId: number
  sessionId: string
  senderType: 'CUSTOMER' | 'BOT' | 'SUPPORT'
  content: string
  createdAtUtc: string
}

export function SupportChatInbox() {
  const [sessions, setSessions] = useState<SessionItem[]>([])
  const [selectedSession, setSelectedSession] = useState<SessionItem | null>(null)
  const [messages, setMessages] = useState<MessageItem[]>([])
  const [filter, setFilter] = useState<'ALL' | 'HUMAN' | 'BOT'>('HUMAN')
  const [replyText, setReplyText] = useState('')
  const [loading, setLoading] = useState(false)
  const [sending, setSending] = useState(false)
  const messagesEndRef = useRef<HTMLDivElement>(null)

  const token = localStorage.getItem('tc_token')

  const fetchSessions = async () => {
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/support/chat/sessions?status=${filter}`, {
        headers: { Authorization: `Bearer ${token}` },
      })
      if (res.ok) {
        const data = await res.json()
        setSessions(data)
        if (data.length > 0 && !selectedSession) {
          setSelectedSession(data[0])
        }
      }
    } catch (e) {
      console.error(e)
    }
  }

  const fetchMessages = async (sessId: string) => {
    setLoading(true)
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/support/chat/sessions/${sessId}/messages`, {
        headers: { Authorization: `Bearer ${token}` },
      })
      if (res.ok) {
        const data = await res.json()
        setMessages(data)
      }
    } catch (e) {
      console.error(e)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchSessions()
    const interval = setInterval(fetchSessions, 5000)
    return () => clearInterval(interval)
  }, [filter])

  useEffect(() => {
    if (selectedSession) {
      fetchMessages(selectedSession.sessionId)
    }
  }, [selectedSession])

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  const handleClaim = async () => {
    if (!selectedSession) return
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/support/chat/sessions/${selectedSession.sessionId}/claim`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
      })
      if (res.ok) {
        setSelectedSession({ ...selectedSession, status: 'HUMAN' })
        fetchSessions()
      }
    } catch (e) {
      console.error(e)
    }
  }

  const handleSendReply = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedSession || !replyText.trim() || sending) return

    setSending(true)
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/support/chat/sessions/${selectedSession.sessionId}/reply`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ message: replyText.trim() }),
      })
      if (res.ok) {
        const newMsg = await res.json()
        setMessages((prev) => [...prev, newMsg])
        setReplyText('')
      }
    } catch (e) {
      console.error(e)
    } finally {
      setSending(false)
    }
  }

  const handleCloseSession = async () => {
    if (!selectedSession) return
    if (!window.confirm('Bạn có chắc muốn đóng phiên tư vấn này?')) return

    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/support/chat/sessions/${selectedSession.sessionId}/close`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` },
      })
      if (res.ok) {
        setSelectedSession({ ...selectedSession, status: 'CLOSED' })
        fetchSessions()
      }
    } catch (e) {
      console.error(e)
    }
  }

  return (
    <div style={{ maxWidth: '1440px', margin: '0 auto', padding: '24px 32px' }}>
      <div style={{ marginBottom: '20px' }}>
        <div className="eyebrow">HỘP THƯ HỖ TRỢ KHÁCH HÀNG (LIVE SUPPORT)</div>
        <h2 style={{ fontSize: '24px', fontWeight: 600, color: 'var(--ink)' }}>Tiếp nhận Chat & Handoff</h2>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '360px 1fr', gap: '20px', minHeight: '600px' }}>
        {/* Left Sidebar: Session List */}
        <div style={{ background: '#ffffff', border: '1px solid var(--line)', borderRadius: '12px', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          {/* Filter Tabs */}
          <div style={{ display: 'flex', borderBottom: '1px solid var(--line)', background: '#fafaf8' }}>
            <button
              type="button"
              onClick={() => setFilter('HUMAN')}
              style={{
                flex: 1,
                padding: '12px 8px',
                border: 'none',
                background: filter === 'HUMAN' ? '#ffffff' : 'transparent',
                fontWeight: filter === 'HUMAN' ? 700 : 500,
                borderBottom: filter === 'HUMAN' ? '2px solid #111' : 'none',
                cursor: 'pointer',
                fontSize: '12px',
              }}
            >
              Cần hỗ trợ ({sessions.filter((s) => s.status === 'HUMAN').length})
            </button>
            <button
              type="button"
              onClick={() => setFilter('BOT')}
              style={{
                flex: 1,
                padding: '12px 8px',
                border: 'none',
                background: filter === 'BOT' ? '#ffffff' : 'transparent',
                fontWeight: filter === 'BOT' ? 700 : 500,
                borderBottom: filter === 'BOT' ? '2px solid #111' : 'none',
                cursor: 'pointer',
                fontSize: '12px',
              }}
            >
              Đang chat với AI
            </button>
            <button
              type="button"
              onClick={() => setFilter('ALL')}
              style={{
                flex: 1,
                padding: '12px 8px',
                border: 'none',
                background: filter === 'ALL' ? '#ffffff' : 'transparent',
                fontWeight: filter === 'ALL' ? 700 : 500,
                borderBottom: filter === 'ALL' ? '2px solid #111' : 'none',
                cursor: 'pointer',
                fontSize: '12px',
              }}
            >
              Tất cả
            </button>
          </div>

          {/* Session Cards */}
          <div style={{ flex: 1, overflowY: 'auto', padding: '8px' }}>
            {sessions.length === 0 ? (
              <div style={{ padding: '32px 16px', textAlign: 'center', color: '#888', fontSize: '13px' }}>
                Không có phiên trò chuyện nào phù hợp.
              </div>
            ) : (
              sessions.map((s) => (
                <div
                  key={s.sessionId}
                  onClick={() => setSelectedSession(s)}
                  style={{
                    padding: '12px 14px',
                    borderRadius: '8px',
                    border: '1px solid ' + (selectedSession?.sessionId === s.sessionId ? '#111111' : '#f0f0ee'),
                    background: selectedSession?.sessionId === s.sessionId ? '#fdfdfc' : '#ffffff',
                    cursor: 'pointer',
                    marginBottom: '8px',
                    boxShadow: selectedSession?.sessionId === s.sessionId ? '0 2px 8px rgba(0,0,0,0.06)' : 'none',
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                    <strong style={{ fontSize: '13px', color: '#111' }}>
                      {s.guestPhone ? `Khách: ${s.guestPhone}` : `Khách #${s.sessionId.slice(0, 8)}`}
                    </strong>
                    <span
                      style={{
                        fontSize: '10px',
                        fontWeight: 700,
                        padding: '2px 6px',
                        borderRadius: '4px',
                        background: s.status === 'HUMAN' ? '#111111' : '#f4f4f2',
                        color: s.status === 'HUMAN' ? '#ffffff' : '#333333',
                        border: s.status === 'HUMAN' ? 'none' : '1px solid #d4d4d0',
                      }}
                    >
                      {s.status === 'HUMAN' ? 'CẦN HỖ TRỢ' : s.status === 'CLOSED' ? 'ĐÃ ĐÓNG' : 'BOT AI'}
                    </span>
                  </div>
                  <div style={{ fontSize: '12px', color: '#666', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {s.lastMessage || 'Chưa có tin nhắn...'}
                  </div>
                  <div style={{ fontSize: '10.5px', color: '#999', marginTop: '6px' }}>
                    Bắt đầu: {new Date(s.startedAtUtc).toLocaleTimeString('vi-VN')}
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Right Pane: Active Thread & Reply Input */}
        <div style={{ background: '#ffffff', border: '1px solid var(--line)', borderRadius: '12px', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          {selectedSession ? (
            <>
              {/* Header */}
              <div style={{ padding: '14px 20px', borderBottom: '1px solid var(--line)', background: '#fafaf8', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div>
                  <h4 style={{ fontSize: '14px', fontWeight: 600, color: '#111' }}>
                    Phiên: {selectedSession.sessionId}
                  </h4>
                  <span style={{ fontSize: '11.5px', color: '#777' }}>
                    SĐT khách: {selectedSession.guestPhone || 'Khách vãng lai'} | Trạng thái: <strong>{selectedSession.status}</strong>
                  </span>
                </div>
                <div style={{ display: 'flex', gap: '8px' }}>
                  {selectedSession.status !== 'HUMAN' && (
                    <button
                      type="button"
                      onClick={handleClaim}
                      style={{ background: '#111111', color: '#fff', border: 'none', padding: '6px 14px', borderRadius: '6px', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
                    >
                      Nhận hỗ trợ (Claim)
                    </button>
                  )}
                  {selectedSession.status !== 'CLOSED' && (
                    <button
                      type="button"
                      onClick={handleCloseSession}
                      style={{ background: '#ffffff', border: '1px solid #dc2626', color: '#dc2626', padding: '6px 12px', borderRadius: '6px', fontSize: '12px', cursor: 'pointer' }}
                    >
                      Đóng phiên
                    </button>
                  )}
                </div>
              </div>

              {/* Messages Body */}
              <div style={{ flex: 1, padding: '20px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '14px', background: '#fcfcfb' }}>
                {loading ? (
                  <div style={{ textAlign: 'center', color: '#888', padding: '24px' }}>Đang tải lịch sử trò chuyện...</div>
                ) : (
                  messages.map((m) => (
                    <div
                      key={m.messageId}
                      style={{
                        alignSelf: m.senderType === 'SUPPORT' ? 'flex-end' : 'flex-start',
                        maxWidth: '75%',
                      }}
                    >
                      <div style={{ fontSize: '10.5px', color: '#777', marginBottom: '3px', fontWeight: 600, textAlign: m.senderType === 'SUPPORT' ? 'right' : 'left' }}>
                        {m.senderType === 'SUPPORT' ? 'Chuyên viên tư vấn' : m.senderType === 'BOT' ? 'Trợ lý AI' : 'Khách hàng'}
                      </div>
                      <div
                        style={{
                          background: m.senderType === 'SUPPORT' ? '#111111' : m.senderType === 'BOT' ? '#f4f4f2' : '#ffffff',
                          color: m.senderType === 'SUPPORT' ? '#ffffff' : '#111111',
                          border: m.senderType === 'SUPPORT' ? 'none' : '1px solid #e4e4e0',
                          borderRadius: '12px',
                          padding: '10px 14px',
                          fontSize: '13px',
                          lineHeight: 1.5,
                          whiteSpace: 'pre-wrap',
                        }}
                      >
                        {m.content}
                      </div>
                      <div style={{ fontSize: '10px', color: '#999', marginTop: '2px', textAlign: m.senderType === 'SUPPORT' ? 'right' : 'left' }}>
                        {new Date(m.createdAtUtc).toLocaleTimeString('vi-VN')}
                      </div>
                    </div>
                  ))
                )}
                <div ref={messagesEndRef} />
              </div>

              {/* Reply Form */}
              <form onSubmit={handleSendReply} style={{ padding: '14px 20px', borderTop: '1px solid var(--line)', background: '#ffffff', display: 'flex', gap: '10px' }}>
                <input
                  type="text"
                  placeholder="Nhập nội dung tư vấn cho khách..."
                  value={replyText}
                  onChange={(e) => setReplyText(e.target.value)}
                  style={{ flex: 1, padding: '10px 14px', border: '1px solid #d4d4d0', borderRadius: '8px', fontSize: '13px', outline: 'none' }}
                />
                <button
                  type="submit"
                  disabled={sending || !replyText.trim()}
                  className="button"
                  style={{ padding: '10px 20px', fontSize: '13px' }}
                >
                  {sending ? 'Đang gửi...' : 'Gửi phản hồi'}
                </button>
              </form>
            </>
          ) : (
            <div style={{ flex: 1, display: 'grid', placeItems: 'center', color: '#888', fontSize: '13.5px' }}>
              Chọn một phiên trò chuyện bên trái để xem nội dung và tương tác.
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

