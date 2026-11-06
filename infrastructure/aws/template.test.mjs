import test from 'node:test'
import assert from 'node:assert/strict'
import vm from 'node:vm'
import { readFileSync } from 'node:fs'
import { template, spaCode } from './build-template.mjs'
const r = template.Resources
test('generated deployment matches its source', () => {
  assert.deepEqual(JSON.parse(readFileSync(new URL('./devflow.template.json', import.meta.url))), template)
})
test('database and application are not publicly reachable', () => {
  assert.equal(r.Database.Properties.PubliclyAccessible, false)
  assert.equal(r.Database.Properties.StorageEncrypted, true)
  assert.equal(r.Database.Properties.DeletionProtection, true)
  assert.equal(r.Database.DeletionPolicy, 'Snapshot')
  assert.equal(r.LoadBalancer.Properties.Scheme, 'internal')
  assert.deepEqual(r.DatabaseSecurityGroup.Properties.SecurityGroupIngress[0].SourceSecurityGroupId, { Ref: 'TaskSecurityGroup' })
  assert.deepEqual(r.TaskSecurityGroup.Properties.SecurityGroupIngress[0].SourceSecurityGroupId, { Ref: 'AlbSecurityGroup' })
})
test('runtime cannot fetch database admin secret; initial deployment cannot run an absent image', () => {
  assert.ok(!JSON.stringify(r.ApiExecutionRole).includes('AdminPassword'))
  assert.ok(!JSON.stringify(r.ApiTask).includes('AdminPassword'))
  assert.equal(template.Parameters.DesiredCount.Default, 0)
  assert.deepEqual(template.Parameters.DesiredCount.AllowedValues, [0, 1])
  assert.equal(r.Service.Properties.DeploymentConfiguration.MaximumPercent, 100)
  assert.equal(r.Repository.Properties.ImageTagMutability, 'IMMUTABLE')
})
test('API forwards auth and websocket inputs without caching or SPA error substitution', () => {
  const config = r.Distribution.Properties.DistributionConfig
  assert.deepEqual(config.CacheBehaviors.map(b => b.PathPattern), ['/api', '/api/*'])
  for (const b of config.CacheBehaviors) {
    assert.equal(b.CachePolicyId, '4135ea2d-6df8-44a3-9df3-4b5a84be39ad')
    assert.equal(b.OriginRequestPolicyId, 'b689b0a8-53d0-40ab-baf2-68738e2966ac')
    assert.equal(b.ViewerProtocolPolicy, 'https-only')
    assert.equal(b.FunctionAssociations, undefined)
  }
  assert.ok(config.CustomErrorResponses.every(e => e.ErrorCachingMinTTL === 0 && !e.ResponsePagePath))
  assert.equal(config.Logging, undefined) // Query access tokens must not enter edge access logs.
  assert.equal(r.FrontendCache.Properties.CachePolicyConfig.MinTTL, 0)
})
test('SPA refresh works without rewriting API or static assets', () => {
  const context = vm.createContext({})
  vm.runInContext(spaCode, context)
  for (const uri of ['/', '/projects', '/projects/one/two', '/auth/callback', '/sign-in']) {
    assert.equal(context.handler({ request: { uri } }).uri, '/index.html')
  }
  for (const uri of ['/api', '/api/tasks', '/api/hubs/projects', '/assets/app.js', '/missing.png']) {
    assert.equal(context.handler({ request: { uri } }).uri, uri)
  }
})
test('all explicit resource and parameter references resolve', () => {
  const allowed = new Set([...Object.keys(r), ...Object.keys(template.Parameters)])
  function walk(value) {
    if (!value || typeof value !== 'object') return
    if (value.Ref && !value.Ref.startsWith('AWS::')) assert.ok(allowed.has(value.Ref), value.Ref)
    if (value['Fn::GetAtt']) assert.ok(Object.hasOwn(r, value['Fn::GetAtt'][0]))
    for (const item of Object.values(value)) walk(item)
  }
  walk(template)
})
