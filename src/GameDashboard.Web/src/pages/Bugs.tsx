import { useEffect, useState } from 'react'
import { api } from '../api/client'

type Game = { gameId: string; displayName: string }
type Report = {
  id: string
  projectId: string
  level: string
  message: string
  status: string
  occurredAt: string
  createdAt: string
  appVersion?: string
}
type ListResp = { items: Report[]; page: number; pageSize: number; total: number }

export default function Bugs() {
  const [games, setGames] = useState<Game[]>([])
  const [gameId, setGameId] = useState('match3')
  const [status, setStatus] = useState('')
  const [list, setList] = useState<ListResp | null>(null)
  const [err, setErr] = useState('')
  const [msg, setMsg] = useState('')

  useEffect(() => {
    api<Game[]>('/api/v1/games')
      .then(g => {
        setGames(g)
        if (g.length && !g.some(x => x.gameId === gameId))
          setGameId(g[0].gameId)
      })
      .catch(() => {})
  }, [])

  async function load(gid = gameId, st = status) {
    setErr('')
    try {
      const q = new URLSearchParams()
      if (gid) q.set('gameId', gid)
      if (st) q.set('status', st)
      q.set('page', '1')
      q.set('pageSize', '50')
      setList(await api<ListResp>(`/api/v1/bugs?${q}`))
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : '加载失败')
    }
  }

  useEffect(() => { load() }, [gameId, status])

  async function setReportStatus(id: string, next: string) {
    try {
      await api(`/api/v1/bugs/${id}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ status: next }),
      })
      setMsg(`已更新 ${id.slice(0, 8)}… → ${next}`)
      load()
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : '更新失败')
    }
  }

  return (
    <>
      <h1>BugReport</h1>
      <p className="muted">对接 BugReport <code>GET /api/v1/reports</code>，projectId = 游戏 game_id</p>

      <div className="card" style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'end' }}>
        <div className="form-row" style={{ margin: 0 }}>
          <label>游戏</label>
          <select value={gameId} onChange={e => setGameId(e.target.value)}>
            {games.length === 0 && <option value="match3">match3</option>}
            {games.map(g => (
              <option key={g.gameId} value={g.gameId}>{g.displayName} ({g.gameId})</option>
            ))}
          </select>
        </div>
        <div className="form-row" style={{ margin: 0 }}>
          <label>状态</label>
          <select value={status} onChange={e => setStatus(e.target.value)}>
            <option value="">全部</option>
            <option value="Open">Open</option>
            <option value="Fixed">Fixed</option>
            <option value="Closed">Closed</option>
          </select>
        </div>
        <button type="button" className="secondary" onClick={() => load()}>刷新</button>
      </div>

      {err && <p className="error">{err}</p>}
      {msg && <p className="ok">{msg}</p>}

      <div className="card">
        <p className="muted">共 {list?.total ?? 0} 条</p>
        <table>
          <thead>
            <tr>
              <th>时间</th>
              <th>级别</th>
              <th>消息</th>
              <th>状态</th>
              <th>版本</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            {(list?.items ?? []).map(r => (
              <tr key={r.id}>
                <td className="muted" style={{ whiteSpace: 'nowrap' }}>{r.occurredAt?.slice(0, 19)}</td>
                <td>{r.level}</td>
                <td>{r.message}<div className="muted" style={{ fontSize: 12 }}>{r.id}</div></td>
                <td><span className="badge">{r.status}</span></td>
                <td className="muted">{r.appVersion}</td>
                <td style={{ whiteSpace: 'nowrap' }}>
                  {r.status !== 'Fixed' && (
                    <button type="button" className="secondary" style={{ marginRight: 4 }}
                      onClick={() => setReportStatus(r.id, 'Fixed')}>Fixed</button>
                  )}
                  {r.status !== 'Closed' && (
                    <button type="button" className="secondary"
                      onClick={() => setReportStatus(r.id, 'Closed')}>Closed</button>
                  )}
                  {r.status !== 'Open' && (
                    <button type="button" className="secondary"
                      onClick={() => setReportStatus(r.id, 'Open')}>Open</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}
