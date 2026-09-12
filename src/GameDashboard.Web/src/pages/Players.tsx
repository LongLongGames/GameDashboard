import { useState } from 'react'
import { api } from '../api/client'

type P = { mpAccountId: string; nickname: string; currencies?: Record<string, number>; extra?: string }

export default function Players() {
  const [q, setQ] = useState('')
  const [list, setList] = useState<P[]>([])
  const [err, setErr] = useState('')

  async function search(e: React.FormEvent) {
    e.preventDefault()
    try {
      setList(await api(`/api/v1/players?gameId=match3&query=${encodeURIComponent(q)}`))
      setErr('')
    } catch (ex: unknown) {
      setErr(ex instanceof Error ? ex.message : '失败')
    }
  }

  return (
    <>
      <h1>玩家</h1>
      <p className="muted">match3 暂无 Admin 全库搜索，列表来自排行榜近似。</p>
      <form className="card" onSubmit={search} style={{ display: 'flex', gap: 8 }}>
        <input value={q} onChange={e => setQ(e.target.value)} placeholder="筛选" style={{ flex: 1 }} />
        <button type="submit">查询</button>
      </form>
      {err && <p className="error">{err}</p>}
      <div className="card">
        <table>
          <thead><tr><th>账号</th><th>昵称</th><th>备注</th></tr></thead>
          <tbody>
            {list.map(p => (
              <tr key={p.mpAccountId}><td>{p.mpAccountId}</td><td>{p.nickname}</td><td className="muted">{p.extra}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}
