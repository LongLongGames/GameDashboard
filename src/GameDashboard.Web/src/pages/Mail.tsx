import { useEffect, useState } from 'react'
import { api } from '../api/client'

type CatalogItem = { id: string; name: string; icon?: string }
type RewardRow = { itemId: string; count: number }
type MailRow = {
  id: string
  projectId: string
  title: string
  targetCount: number
  isBroadcast: boolean
  senderName?: string
  createdAt?: string
}

export default function Mail() {
  const [mode, setMode] = useState<'single' | 'multi' | 'all'>('single')
  const [mpAccountId, setId] = useState('')
  const [targetIdsText, setTargets] = useState('')
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [senderName, setSender] = useState('GM')
  const [expireHours, setExpireHours] = useState(0)
  const [catalog, setCatalog] = useState<CatalogItem[]>([])
  const [rewards, setRewards] = useState<RewardRow[]>([])
  const [pickId, setPickId] = useState('')
  const [pickCount, setPickCount] = useState(1)
  const [msg, setMsg] = useState('')
  const [err, setErr] = useState('')
  const [list, setList] = useState<MailRow[]>([])
  const [busy, setBusy] = useState(false)

  async function loadCatalog() {
    try {
      const r = await api<{ items: CatalogItem[] }>('/api/v1/mail/item-catalog?gameId=match3')
      setCatalog(r.items || [])
      if (r.items?.length && !pickId) setPickId(r.items[0].id)
    } catch (ex: unknown) {
      setErr(ex instanceof Error ? ex.message : '加载道具表失败')
    }
  }

  async function loadList() {
    try {
      const r = await api<{ items: MailRow[]; total: number }>('/api/v1/mail/list?gameId=match3&page=1&pageSize=30')
      setList(r.items || [])
    } catch {
      /* ignore */
    }
  }

  useEffect(() => {
    loadCatalog()
    loadList()
  }, [])

  function addReward() {
    if (!pickId) {
      setErr('请选择配置表中的道具')
      return
    }
    if (pickCount < 1) {
      setErr('数量必须 ≥ 1')
      return
    }
    if (!catalog.some(c => c.id === pickId)) {
      setErr(`道具 ${pickId} 不在配置表中`)
      return
    }
    setRewards(prev => {
      const i = prev.findIndex(x => x.itemId === pickId)
      if (i >= 0) {
        const next = [...prev]
        next[i] = { itemId: pickId, count: next[i].count + pickCount }
        return next
      }
      return [...prev, { itemId: pickId, count: pickCount }]
    })
    setErr('')
  }

  function removeReward(itemId: string) {
    setRewards(prev => prev.filter(x => x.itemId !== itemId))
  }

  async function send(e: React.FormEvent) {
    e.preventDefault()
    setMsg('')
    setErr('')
    if (!title.trim() || !body.trim()) {
      setErr('标题与内容必填')
      return
    }
    if (mode === 'single' && !mpAccountId.trim()) {
      setErr('请填写 mp_account_id')
      return
    }
    if (mode === 'multi' && !targetIdsText.trim()) {
      setErr('请填写收件人列表')
      return
    }
    if (mode === 'all') {
      if (!window.confirm('确认向【全服】发送补偿邮件？玩家打开收件箱时会自动收到。')) return
    }

    setBusy(true)
    try {
      const r = await api<{ message: string }>('/api/v1/mail/send', {
        method: 'POST',
        body: JSON.stringify({
          gameId: 'match3',
          mode,
          mpAccountId: mpAccountId.trim() || null,
          targetIdsText: targetIdsText || null,
          title: title.trim(),
          body: body.trim(),
          rewards,
          senderName: senderName.trim() || 'GM',
          expireHours: expireHours > 0 ? expireHours : null,
        }),
      })
      setMsg(r.message)
      loadList()
    } catch (ex: unknown) {
      setErr(ex instanceof Error ? ex.message : '发送失败')
    } finally {
      setBusy(false)
    }
  }

  const nameOf = (id: string) => catalog.find(c => c.id === id)?.name || id

  return (
    <>
      <h1>GM 邮件</h1>
      <p className="muted">
        直连 Mail 服务发信；奖励来自 config/match3/Item.json（Excel 导表产物），禁止手填 Id。全服=广播，玩家打开收件箱时懒分发。
      </p>

      <form className="card" onSubmit={send}>
        <div className="form-row">
          <label>发放范围</label>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <label><input type="radio" checked={mode === 'single'} onChange={() => setMode('single')} /> 指定玩家</label>
            <label><input type="radio" checked={mode === 'multi'} onChange={() => setMode('multi')} /> 批量玩家</label>
            <label><input type="radio" checked={mode === 'all'} onChange={() => setMode('all')} /> 全服补偿</label>
          </div>
        </div>

        {mode === 'single' && (
          <div className="form-row">
            <label>mp_account_id</label>
            <input value={mpAccountId} onChange={e => setId(e.target.value)} placeholder="玩家 UUID" />
          </div>
        )}
        {mode === 'multi' && (
          <div className="form-row">
            <label>收件人（每行一个，或逗号分隔）</label>
            <textarea value={targetIdsText} onChange={e => setTargets(e.target.value)} rows={4} placeholder={"uuid1\nuuid2"} />
          </div>
        )}
        {mode === 'all' && (
          <p className="muted">将创建广播邮件：所有登录玩家在打开收件箱时自动入队，无需枚举账号。</p>
        )}

        <div className="form-row">
          <label>标题</label>
          <input value={title} onChange={e => setTitle(e.target.value)} maxLength={80} />
        </div>
        <div className="form-row">
          <label>内容</label>
          <textarea value={body} onChange={e => setBody(e.target.value)} rows={4} />
        </div>
        <div className="form-row">
          <label>发件人显示名</label>
          <input value={senderName} onChange={e => setSender(e.target.value)} />
        </div>
        <div className="form-row">
          <label>过期（小时，0=不过期）</label>
          <input type="number" min={0} value={expireHours} onChange={e => setExpireHours(Number(e.target.value) || 0)} />
        </div>

        <div className="form-row">
          <label>奖励（仅配置表内道具）</label>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
            <select value={pickId} onChange={e => setPickId(e.target.value)} style={{ minWidth: 160 }}>
              {catalog.map(c => (
                <option key={c.id} value={c.id}>{c.name} ({c.id})</option>
              ))}
            </select>
            <input type="number" min={1} value={pickCount} onChange={e => setPickCount(Math.max(1, Number(e.target.value) || 1))} style={{ width: 80 }} />
            <button type="button" className="secondary" onClick={addReward}>添加</button>
          </div>
          {rewards.length === 0 ? (
            <p className="muted">无附件（纯文本通知）</p>
          ) : (
            <ul style={{ margin: '0.5rem 0', paddingLeft: 18 }}>
              {rewards.map(r => (
                <li key={r.itemId}>
                  {nameOf(r.itemId)} × {r.count}{' '}
                  <button type="button" className="secondary" style={{ padding: '0.15rem 0.5rem' }} onClick={() => removeReward(r.itemId)}>移除</button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <button type="submit" disabled={busy}>{busy ? '发送中…' : '发送邮件'}</button>
        {msg && <p className="ok">{msg}</p>}
        {err && <p className="error">{err}</p>}
      </form>

      <div className="card">
        <h2>最近发送</h2>
        <button type="button" className="secondary" onClick={loadList}>刷新</button>
        {list.length === 0 ? (
          <p className="muted">暂无记录（需 Mail 服务已启动并配置 AdminApiKey）</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>标题</th>
                <th>范围</th>
                <th>发件人</th>
                <th>时间</th>
              </tr>
            </thead>
            <tbody>
              {list.map(m => (
                <tr key={m.id}>
                  <td>{m.title}</td>
                  <td>{m.isBroadcast ? <span className="badge ok">全服</span> : `${m.targetCount} 人`}</td>
                  <td>{m.senderName || '—'}</td>
                  <td className="muted">{m.createdAt ? new Date(m.createdAt).toLocaleString() : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  )
}
