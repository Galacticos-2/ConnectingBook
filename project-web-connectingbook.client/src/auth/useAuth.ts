import { useContext, useSyncExternalStore } from 'react'
import { getSession, subscribe } from '../api/client'
import { AuthContext } from './context'

export function useAuth() {
  return { ...useContext(AuthContext), session: useSyncExternalStore(subscribe, getSession) }
}
