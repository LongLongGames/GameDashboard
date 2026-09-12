import { useEffect, useState } from 'react'
import { api } from '../api/client'

type Status = {
  health: { ok: boolean; raw?: string; message: string }
  version?: string
  top: { mpAccountId: string; nickname?: string; score: number; rank: number }[]
  jwtPlayer?: { mpAccountId: string; nickname: string; currencies?: Record<string, number> }
}

export default function Match3() {
  const [st, setSt] = useState<Status | null>(null)
  const [msg, setMsg] = useState('')
  const load = () => api<Status>('/api/v1/match3/status').then(setSt).catch(e => setMsg(e.message))
  useEffect(() => { load() }, [])

  async function refill() {
    try {
      const r = await api<{ message: string }>('/api/v1/match3/energy-refill', { method: 'POST' })
      setMsg(r.message)
      load()
    } catch (e: unknown) {
      setMsg(e instanceof Error ? e.message : '失败')
    }
  }

  return (
    <>
      <h1>Match3 闭环</h1>
      {msg && <p className="muted">{msg}</p>}
      <div className="card">
        <h2>/health</h2>
        {st && (
          <p>
            <span className={`badge ${st.health.ok ? 'ok' : 'fail'}`}>{st.health.ok ? 'OK' : 'FAIL'}</span>
            {' '}{st.health.message}
          </p>
        )}
        <pre className="muted" style={{ fontSize: 12 }}>{st?.health.raw}</pre>
      </div>
      <div className="card">
        <h2>版本检查</h2>
        <pre className="muted" style={{ fontSize: 12, maxHeight: 160, overflow: 'auto' }}>{st?.version}</pre>
      </div>
      <div className="card">
        <h2>排行榜 Top</h2>
        <table>
          <thead><tr><th>#</th><th>账号</th><th>昵称</th><th>分数</th></tr></thead>
          <tbody>
            {st?.top.map(e => (
              <tr key={e.mpAccountId}><td>{e.rank}</td><td>{e.mpAccountId}</td><td>{e.nickname}</td><td>{e.score}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="card">
        <h2>DebugJwt 玩家</h2>
        {st?.jwtPlayer ? (
          <>
            <p>{st.jwtPlayer.nickname} ({st.jwtPlayer.mpAccountId})</p>
            <p className="muted">{JSON.stringify(st.jwtPlayer.currencies)}</p>
            <button type="button" onClick={refill}>体力回满</button>
          </>
        ) : (
          <p className="muted">配置 Games:Match3:DebugJwt 后可查看并 cheat-refill</p>
        )}
      </div>
    </>
  )
}
