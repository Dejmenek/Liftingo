-- Counts a failed login for an e-mail address that has no account (R21).
-- KEYS[1] = liftingo:login-throttle:{hash of the address}
-- ARGV[1] = now (Unix ms), ARGV[2] = failures that trigger a lockout, ARGV[3] = lockout duration (ms)
-- Returns -1 when the address is locked (the attempt is not counted), the lockout end (Unix ms) when this
-- failure triggered the lockout, and 0 otherwise.
local now = tonumber(ARGV[1])
local lockoutEnd = tonumber(redis.call('HGET', KEYS[1], 'lockoutEnd'))

if lockoutEnd and lockoutEnd > now then
  return -1
end

redis.call('HDEL', KEYS[1], 'lockoutEnd')
local failures = redis.call('HINCRBY', KEYS[1], 'failures', 1)

if failures < tonumber(ARGV[2]) then
  -- The counter does not decay (FR-012), so the entry has no TTL while it counts.
  redis.call('PERSIST', KEYS[1])
  return 0
end

-- The lockout resets the counter. The entry goes away when the lockout ends, with nothing left to remember.
local endsAt = now + tonumber(ARGV[3])
redis.call('HSET', KEYS[1], 'failures', 0, 'lockoutEnd', endsAt)
redis.call('PEXPIRE', KEYS[1], ARGV[3])

return endsAt
