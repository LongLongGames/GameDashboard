import { useState } from 'react'
import { api } from '../api/client'

export default function Mail() {
  const [mpAccountId, setId] = useState('')
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [msg, setMsg] = useState('')

  async function send(e: React.FormEvent) {
    e.preventDefault()
    try {
      const r = await api<{ message: string }>('/api/v1/mail/send', {
        method: 'POST',
        body: JSON.stringify({ gameId: 'match3', mpAccountId, title, body }),
      })
      setMsg(r.message)
    } catch (ex: unknown) {
      setMsg(ex instanceof Error ? ex.message : '失败')
    }
  }

  return (
    <>
      <h1>GM 邮件</h1>
      <p className="muted">需游戏暴露 /admin/v1/mail/send</p>
      <form className="card" onSubmit={send}>
        <div className="form-row"><label>mp_account_id</label><input value={mpAccountId} onChange={e => setId(e.target.value)} /></div>
        <div className="form-row"><label>标题</label><input value={title} onChange={e => setTitle(e.target.value)} /></div>
        <div className="form-row"><label>内容</label><textarea value={body} onChange={e => setBody(e.target.value)} /></div>
        <button type="submit">发送</button>
        {msg && <p className="muted">{msg}</p>}
      </form>
    </>
  )
}
