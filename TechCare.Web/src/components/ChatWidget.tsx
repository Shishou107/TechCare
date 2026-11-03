import React, { useState, useEffect, useRef } from 'react'
import { API_BASE_URL } from '../api/client'

interface Citation {
  title: string
  snippet: string
}

interface Message {
  id: string | number
  sender: 'CUSTOMER' | 'BOT' | 'SUPPORT'
  text: string
  sources?: Citation[]
  time: string
}

export function ChatWidget() {
  const [isOpen, setIsOpen] = useState(false)
  const [sessionId, setSessionId] = useState<string>(() => {
    return localStorage.getItem('tc_chat_session_id') || ''
  })
  const [messages, setMessages] = useState<Message[]>([
    {
      id: 'welcome',
      sender: 'BOT',
      text: 'Xin chào! Em là trợ lý AI của TechCare. Em có thể hỗ trợ tra cứu giá linh kiện, quy trình bảo hành, sửa chữa hoặc kết nối trực tiếp với chuyên viên ạ.',
      time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }),
    },
  ])
  const [input, setInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [isHumanMode, setIsHumanMode] = useState(false)
  const [expandedCitation, setExpandedCitation] = useState<number | null>(null)
  const messagesEndRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (sessionId) {
      localStorage.setItem('tc_chat_session_id', sessionId)
    }
  }, [sessionId])

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, loading])

  const handleSend = async (e: React.FormEvent) => {
    e.preventDefault()
    const text = input.trim()
    if (!text || loading) return

    const now = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
    const userMsg: Message = {
      id: Date.now(),
      sender: 'CUSTOMER',
      text,
      time: now,
    }

    setMessages((prev) => [...prev, userMsg])
    setInput('')
    setLoading(true)

    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/chat/message`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sessionId: sessionId || null,
          message: text,
        }),
      })

      if (res.ok) {
        const data = await res.json()
        if (data.sessionId && !sessionId) {
          setSessionId(data.sessionId)
        }
        if (data.status === 'HUMAN') {
          setIsHumanMode(true)
        }

        const botMsg: Message = {
          id: Date.now() + 1,
          sender: data.status === 'HUMAN' && isHumanMode ? 'SUPPORT' : 'BOT',
          text: data.reply,
          sources: data.sources && data.sources.length > 0 ? data.sources : undefined,
          time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }),
        }
        setMessages((prev) => [...prev, botMsg])
      } else {
        setMessages((prev) => [
          ...prev,
          {
            id: Date.now() + 1,
            sender: 'BOT',
            text: 'Hiện tại hệ thống đang bận. Quý khách vui lòng thử lại sau giây lát hoặc liên hệ hotline 1900-TECHCARE.',
            time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }),
          },
        ])
      }
    } catch {
      setMessages((prev) => [
        ...prev,
        {
          id: Date.now() + 1,
          sender: 'BOT',
          text: 'Không thể kết nối đến máy chủ TechCare. Vui lòng kiểm tra lại kết nối mạng.',
          time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }),
        },
      ])
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="tc-chat-widget-root" style={{ position: 'fixed', bottom: '24px', right: '24px', zIndex: 9999, fontFamily: 'inherit' }}>
      {/* Floating Toggle Button */}
      {!isOpen && (
        <button
          type="button"
          onClick={() => setIsOpen(true)}
          style={{
            background: '#111111',
            color: '#ffffff',
            border: 'none',
            borderRadius: '999px',
            padding: '12px 20px',
            display: 'flex',
            alignItems: 'center',
            gap: '10px',
            fontSize: '13px',
            fontWeight: 600,
            cursor: 'pointer',
            boxShadow: '0 4px 18px rgba(0,0,0,0.22)',
            transition: 'transform 0.2s ease',
          }}
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
          </svg>
          <span>Hỗ trợ & Báo giá 24/7</span>
        </button>
      )}

      {/* Expanded Chat Box */}
      {isOpen && (
        <div
          style={{
            width: '360px',
            height: '520px',
            background: '#ffffff',
            border: '1.5px solid #111111',
            borderRadius: '16px',
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
            boxShadow: '0 8px 30px rgba(0,0,0,0.18)',
          }}
        >
          {/* Header */}
          <div
            style={{
              background: '#111111',
              color: '#ffffff',
              padding: '14px 16px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
            }}
          >
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span style={{ width: '8px', height: '8px', borderRadius: '50%', background: '#ffffff', display: 'inline-block' }}></span>
                <strong style={{ fontSize: '14px' }}>TechCare AI Assistant</strong>
              </div>
              <div style={{ fontSize: '11px', color: '#aaaaaa', marginTop: '2px' }}>
                {isHumanMode ? 'Đang kết nối chuyên viên tư vấn' : 'Hệ thống tri thức RAG & Báo giá tự động'}
              </div>
            </div>

            <button
              type="button"
              onClick={() => setIsOpen(false)}
              style={{
                background: 'transparent',
                border: 'none',
                color: '#ffffff',
                fontSize: '18px',
                cursor: 'pointer',
                lineHeight: 1,
                padding: '4px',
              }}
            >
              ✕
            </button>
          </div>

          {/* Mode Banner if Human */}
          {isHumanMode && (
            <div style={{ background: '#f4f4f2', borderBottom: '1px solid #e4e4e0', padding: '6px 12px', fontSize: '11px', color: '#333', display: 'flex', alignItems: 'center', gap: '6px' }}>
              <span style={{ fontWeight: 700 }}>●</span>
              <span>Cuộc trò chuyện đã được chuyển tới chuyên viên tư vấn.</span>
            </div>
          )}

          {/* Messages Body */}
          <div
            style={{
              flex: 1,
              padding: '14px',
              overflowY: 'auto',
              display: 'flex',
              flexDirection: 'column',
              gap: '12px',
              background: '#fcfcfb',
            }}
          >
            {messages.map((m, idx) => (
              <div
                key={m.id}
                style={{
                  alignSelf: m.sender === 'CUSTOMER' ? 'flex-end' : 'flex-start',
                  maxWidth: '85%',
                }}
              >
                {m.sender !== 'CUSTOMER' && (
                  <div style={{ fontSize: '10.5px', color: '#777', marginBottom: '3px', fontWeight: 600 }}>
                    {m.sender === 'SUPPORT' ? 'Chuyên viên TechCare' : 'TechCare AI'}
                  </div>
                )}
                <div
                  style={{
                    background: m.sender === 'CUSTOMER' ? '#111111' : '#ffffff',
                    color: m.sender === 'CUSTOMER' ? '#ffffff' : '#111111',
                    border: m.sender === 'CUSTOMER' ? 'none' : '1px solid #e4e4e0',
                    borderRadius: '12px',
                    padding: '10px 14px',
                    fontSize: '12.5px',
                    lineHeight: 1.5,
                    whiteSpace: 'pre-wrap',
                  }}
                >
                  {m.text}

                  {/* Sources Citations Pill */}
                  {m.sources && m.sources.length > 0 && (
                    <div style={{ marginTop: '8px', paddingTop: '6px', borderTop: '1px dashed #d4d4d0' }}>
                      <div style={{ fontSize: '10px', color: '#666', fontWeight: 600, marginBottom: '4px' }}>
                        Nguồn đối chiếu:
                      </div>
                      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '4px' }}>
                        {m.sources.map((s, sIdx) => (
                          <div key={sIdx}>
                            <button
                              type="button"
                              onClick={() => setExpandedCitation(expandedCitation === idx * 10 + sIdx ? null : idx * 10 + sIdx)}
                              style={{
                                background: '#f4f4f2',
                                border: '1px solid #d4d4d0',
                                borderRadius: '4px',
                                padding: '2px 6px',
                                fontSize: '10px',
                                color: '#111',
                                cursor: 'pointer',
                              }}
                            >
                              📄 {s.title}
                            </button>
                            {expandedCitation === idx * 10 + sIdx && (
                              <div style={{ marginTop: '4px', padding: '6px 8px', background: '#f9f9f8', border: '1px solid #e4e4e0', borderRadius: '4px', fontSize: '11px', color: '#555' }}>
                                {s.snippet}
                              </div>
                            )}
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
                <div
                  style={{
                    fontSize: '10px',
                    color: '#999',
                    marginTop: '3px',
                    textAlign: m.sender === 'CUSTOMER' ? 'right' : 'left',
                  }}
                >
                  {m.time}
                </div>
              </div>
            ))}

            {loading && (
              <div style={{ alignSelf: 'flex-start', padding: '8px 12px', background: '#f4f4f2', borderRadius: '10px', fontSize: '11.5px', color: '#666' }}>
                Đang tra cứu cơ sở tri thức...
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>

          {/* Quick Questions suggestion */}
          <div style={{ padding: '6px 12px', background: '#fafaf8', borderTop: '1px solid #e4e4e0', display: 'flex', gap: '6px', overflowX: 'auto', whiteSpace: 'nowrap' }}>
            <button
              type="button"
              onClick={() => setInput('Thay màn hình laptop Dell giá bao nhiêu?')}
              style={{ background: '#ffffff', border: '1px solid #d4d4d0', borderRadius: '999px', padding: '3px 8px', fontSize: '11px', color: '#333', cursor: 'pointer' }}
            >
              Giá thay màn hình?
            </button>
            <button
              type="button"
              onClick={() => setInput('Quy trình bảo hành chống tráo linh kiện ra sao?')}
              style={{ background: '#ffffff', border: '1px solid #d4d4d0', borderRadius: '999px', padding: '3px 8px', fontSize: '11px', color: '#333', cursor: 'pointer' }}
            >
              Chống tráo linh kiện?
            </button>
            <button
              type="button"
              onClick={() => setInput('Em muốn gặp nhân viên tư vấn trực tiếp.')}
              style={{ background: '#ffffff', border: '1px solid #d4d4d0', borderRadius: '999px', padding: '3px 8px', fontSize: '11px', color: '#333', cursor: 'pointer' }}
            >
              Gặp nhân viên
            </button>
          </div>

          {/* Input Footer */}
          <form
            onSubmit={handleSend}
            style={{
              padding: '10px 12px',
              borderTop: '1px solid #e4e4e0',
              display: 'flex',
              gap: '8px',
              background: '#ffffff',
            }}
          >
            <input
              type="text"
              placeholder="Nhập câu hỏi của bạn..."
              value={input}
              onChange={(e) => setInput(e.target.value)}
              style={{
                flex: 1,
                border: '1px solid #d4d4d0',
                borderRadius: '8px',
                padding: '8px 12px',
                fontSize: '12.5px',
                outline: 'none',
              }}
            />
            <button
              type="submit"
              disabled={loading || !input.trim()}
              style={{
                background: '#111111',
                color: '#ffffff',
                border: 'none',
                borderRadius: '8px',
                padding: '8px 14px',
                fontSize: '12.5px',
                fontWeight: 600,
                cursor: loading || !input.trim() ? 'not-allowed' : 'pointer',
                opacity: loading || !input.trim() ? 0.5 : 1,
              }}
            >
              Gửi
            </button>
          </form>
        </div>
      )}
    </div>
  )
}

