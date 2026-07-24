import { createFileRoute } from '@tanstack/react-router'
import { EditorLayout } from '../components/layout/EditorLayout'

export const Route = createFileRoute('/')({ component: Home })

function Home() {
  return <EditorLayout />
}
