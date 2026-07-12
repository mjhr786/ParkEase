import React, { useEffect, useState } from 'react';
import { useCompany } from '../../contexts/CompanyContext';
import corporateService from '../../services/corporateService';
import toast from 'react-hot-toast';
import { useNavigate } from 'react-router-dom';

const CompanyMembers = () => {
    const { activeCompanyId, isCorporateMode } = useCompany();
    const navigate = useNavigate();

    const [members, setMembers] = useState([]);
    const [loading, setLoading] = useState(true);

    // Add / invite modal
    const [showInviteModal, setShowInviteModal] = useState(false);
    const [inviteMode, setInviteMode] = useState('auto'); // auto | existing | invite
    const [inviteEmail, setInviteEmail] = useState('');
    const [inviteRole, setInviteRole] = useState(1); // 1 = Employee
    const [invitePriority, setInvitePriority] = useState(1);
    const [inviteEmployeeCode, setInviteEmployeeCode] = useState('');
    const [inviting, setInviting] = useState(false);
    const [lastInviteLink, setLastInviteLink] = useState(null);

    // Edit modal
    const [editMember, setEditMember] = useState(null);
    const [savingMember, setSavingMember] = useState(false);

    useEffect(() => {
        if (!isCorporateMode) {
            navigate('/dashboard', { replace: true });
            return;
        }
        loadMembers();
    }, [activeCompanyId, isCorporateMode, navigate]);

    const loadMembers = async () => {
        setLoading(true);
        try {
            const response = await corporateService.getMembers(1, 100);
            if (response.success && response.data) {
                setMembers(response.data.members || []);
            } else {
                toast.error(response.message || 'Failed to load members');
            }
        } catch {
            toast.error('Could not reach server');
        } finally {
            setLoading(false);
        }
    };

    const buildInviteLink = (token) => {
        if (!token) return null;
        return `${window.location.origin}/invite/accept/${encodeURIComponent(token)}`;
    };

    const copyText = async (text) => {
        try {
            await navigator.clipboard.writeText(text);
            toast.success('Invite link copied');
        } catch {
            toast.error('Could not copy — select and copy manually');
        }
    };

    const resetInviteForm = () => {
        setInviteEmail('');
        setInviteRole(1);
        setInvitePriority(1);
        setInviteEmployeeCode('');
        setInviteMode('auto');
        setLastInviteLink(null);
    };

    const handleInvite = async (e) => {
        e.preventDefault();
        setInviting(true);
        setLastInviteLink(null);
        const email = inviteEmail.trim();
        const role = parseInt(inviteRole, 10);
        const priority = parseInt(invitePriority, 10) || 1;
        const employeeCode = inviteEmployeeCode.trim() || null;

        try {
            // Prefer direct add for existing ParkEase accounts (hybrid registration).
            if (inviteMode === 'existing' || inviteMode === 'auto') {
                const addRes = await corporateService.addMember({
                    email,
                    role,
                    priority,
                    employeeCode,
                });
                if (addRes.success) {
                    toast.success(addRes.message || 'Existing user added as member');
                    setShowInviteModal(false);
                    resetInviteForm();
                    loadMembers();
                    return;
                }
                if (inviteMode === 'existing') {
                    toast.error(addRes.message || 'Could not add existing user');
                    return;
                }
                // auto: fall through to invite when user does not exist
                if (!addRes.message?.toLowerCase().includes('no parkease user')) {
                    toast.error(addRes.message || 'Failed to add member');
                    return;
                }
            }

            const response = await corporateService.inviteMember({ email, role });
            if (response.success) {
                const link = buildInviteLink(response.data?.invitationToken);
                setLastInviteLink(link);
                toast.success(response.message || 'Invitation created');
                if (link) {
                    // Keep modal open so admin can copy the link
                } else {
                    setShowInviteModal(false);
                    resetInviteForm();
                }
            } else {
                toast.error(response.message || 'Failed to send invitation');
            }
        } catch (err) {
            toast.error(err?.message || 'An error occurred during invitation');
        } finally {
            setInviting(false);
        }
    };

    const openEdit = (member) => {
        setEditMember({
            id: member.id,
            userName: member.userName,
            userEmail: member.userEmail,
            role: member.role ?? 1,
            priority: member.priority ?? 1,
            employeeCode: member.employeeCode || '',
        });
    };

    const handleUpdateMember = async (e) => {
        e.preventDefault();
        if (!editMember) return;
        const priority = parseInt(editMember.priority, 10);
        if (Number.isNaN(priority) || priority < 1 || priority > 10) {
            toast.error('Priority must be between 1 and 10.');
            return;
        }
        setSavingMember(true);
        try {
            const payload = {
                role: parseInt(editMember.role, 10),
                priority,
                employeeCode: editMember.employeeCode?.trim() || null,
                clearEmployeeCode: !editMember.employeeCode?.trim(),
            };
            const response = await corporateService.updateMember(editMember.id, payload);
            if (response.success) {
                toast.success(response.message || 'Member updated');
                setEditMember(null);
                loadMembers();
            } else {
                toast.error(response.message || 'Failed to update member');
            }
        } catch (err) {
            toast.error(err?.message || 'An error occurred while updating the member');
        } finally {
            setSavingMember(false);
        }
    };

    const handleRemove = async (membershipId) => {
        if (!window.confirm('Are you sure you want to remove this member?')) return;

        try {
            const response = await corporateService.removeMember(membershipId);
            if (response.success) {
                toast.success('Member removed');
                loadMembers();
            } else {
                toast.error(response.message || 'Failed to remove member');
            }
        } catch {
            toast.error('An error occurred during removal');
        }
    };

    if (!isCorporateMode) return null;

    return (
        <div className="container" style={{ padding: '2rem 0', color: '#f1f5f9' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem', flexWrap: 'wrap', gap: '1rem' }}>
                <div>
                    <h1 style={{ color: 'white', display: 'flex', alignItems: 'center', gap: '10px', margin: '0 0 0.35rem 0' }}>
                        <span style={{ fontSize: '2rem' }}>👥</span> Company Members
                    </h1>
                    <p style={{ color: '#94a3b8', margin: 0, fontSize: '0.9rem' }}>
                        Manage roles and booking priority (1–10). Higher priority ranks first on the waitlist.
                    </p>
                </div>
                <button
                    onClick={() => { resetInviteForm(); setShowInviteModal(true); }}
                    className="btn btn-primary"
                    style={{ padding: '0.6rem 1.2rem', display: 'flex', alignItems: 'center', gap: '8px' }}
                >
                    <span>✉️</span> Add / Invite
                </button>
            </div>

            <div style={{ background: '#1e293b', borderRadius: '12px', border: '1px solid rgba(255,255,255,0.05)', overflow: 'hidden' }}>
                {loading ? (
                    <div style={{ padding: '3rem', textAlign: 'center' }}><div className="spinner" /></div>
                ) : members.length > 0 ? (
                    <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
                        <thead style={{ background: 'rgba(255,255,255,0.02)', borderBottom: '1px solid rgba(255,255,255,0.05)' }}>
                            <tr>
                                <th style={{ padding: '1rem', color: '#94a3b8', fontWeight: '600', fontSize: '0.85rem', textTransform: 'uppercase' }}>Name</th>
                                <th style={{ padding: '1rem', color: '#94a3b8', fontWeight: '600', fontSize: '0.85rem', textTransform: 'uppercase' }}>Email</th>
                                <th style={{ padding: '1rem', color: '#94a3b8', fontWeight: '600', fontSize: '0.85rem', textTransform: 'uppercase' }}>Role</th>
                                <th style={{ padding: '1rem', color: '#94a3b8', fontWeight: '600', fontSize: '0.85rem', textTransform: 'uppercase' }}>Priority</th>
                                <th style={{ padding: '1rem', color: '#94a3b8', fontWeight: '600', fontSize: '0.85rem', textTransform: 'uppercase' }}>Status</th>
                                <th style={{ padding: '1rem', color: '#94a3b8', fontWeight: '600', fontSize: '0.85rem', textTransform: 'uppercase', textAlign: 'right' }}>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {members.map((member) => (
                                <tr key={member.id} style={{ borderBottom: '1px solid rgba(255,255,255,0.05)' }}>
                                    <td style={{ padding: '1rem' }}>
                                        <div style={{ fontWeight: '500' }}>{member.userName}</div>
                                        {member.employeeCode && <div style={{ fontSize: '0.75rem', color: '#64748b' }}>Code: {member.employeeCode}</div>}
                                    </td>
                                    <td style={{ padding: '1rem', color: '#cbd5e1' }}>{member.userEmail}</td>
                                    <td style={{ padding: '1rem' }}>
                                        <span style={{
                                            background: member.role === 0 ? 'rgba(56, 189, 248, 0.1)' : 'rgba(148, 163, 184, 0.1)',
                                            color: member.role === 0 ? '#38bdf8' : '#94a3b8',
                                            padding: '4px 8px', borderRadius: '4px', fontSize: '0.8rem', fontWeight: '600'
                                        }}>
                                            {member.role === 0 ? 'Admin' : 'Employee'}
                                        </span>
                                    </td>
                                    <td style={{ padding: '1rem', color: '#cbd5e1' }}>{member.priority}</td>
                                    <td style={{ padding: '1rem' }}>
                                        <span style={{
                                            background: member.isActive ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)',
                                            color: member.isActive ? '#10b981' : '#ef4444',
                                            padding: '4px 8px', borderRadius: '4px', fontSize: '0.8rem', fontWeight: '600'
                                        }}>
                                            {member.isActive ? 'Active' : 'Inactive'}
                                        </span>
                                    </td>
                                    <td style={{ padding: '1rem', textAlign: 'right' }}>
                                        <div style={{ display: 'inline-flex', gap: '0.5rem' }}>
                                            <button
                                                type="button"
                                                onClick={() => openEdit(member)}
                                                style={{
                                                    background: 'transparent',
                                                    border: '1px solid rgba(56,189,248,0.35)',
                                                    color: '#38bdf8',
                                                    padding: '6px 12px',
                                                    borderRadius: '6px',
                                                    cursor: 'pointer',
                                                }}
                                            >
                                                Edit
                                            </button>
                                            <button
                                                type="button"
                                                onClick={() => handleRemove(member.id)}
                                                style={{
                                                    background: 'transparent',
                                                    border: '1px solid rgba(239,68,68,0.3)',
                                                    color: '#ef4444',
                                                    padding: '6px 12px',
                                                    borderRadius: '6px',
                                                    cursor: 'pointer',
                                                }}
                                            >
                                                Remove
                                            </button>
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                ) : (
                    <div style={{ padding: '3rem', textAlign: 'center', color: '#94a3b8' }}>
                        <p>No members found. Send some invites to get started!</p>
                    </div>
                )}
            </div>

            {/* Add / Invite Modal */}
            {showInviteModal && (
                <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.7)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem' }}>
                    <div style={{ background: '#1e293b', width: '100%', maxWidth: '460px', borderRadius: '12px', padding: '2rem', border: '1px solid rgba(255,255,255,0.1)' }}>
                        <h2 style={{ marginBottom: '0.35rem', color: 'white' }}>Add or invite member</h2>
                        <p style={{ color: '#94a3b8', fontSize: '0.85rem', marginBottom: '1.25rem' }}>
                            Existing ParkEase users are added immediately. New emails get an invitation link.
                        </p>

                        {lastInviteLink ? (
                            <div>
                                <p style={{ color: '#6ee7b7', marginBottom: '0.75rem' }}>Invitation created. Share this link:</p>
                                <div style={{
                                    background: '#0f172a',
                                    border: '1px solid #334155',
                                    borderRadius: '8px',
                                    padding: '0.75rem',
                                    color: '#cbd5e1',
                                    fontSize: '0.8rem',
                                    wordBreak: 'break-all',
                                    marginBottom: '1rem',
                                }}
                                >
                                    {lastInviteLink}
                                </div>
                                <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end', flexWrap: 'wrap' }}>
                                    <button type="button" className="btn btn-secondary" onClick={() => copyText(lastInviteLink)}>
                                        Copy link
                                    </button>
                                    <button
                                        type="button"
                                        className="btn btn-primary"
                                        onClick={() => { setShowInviteModal(false); resetInviteForm(); }}
                                    >
                                        Done
                                    </button>
                                </div>
                            </div>
                        ) : (
                            <form onSubmit={handleInvite}>
                                <div className="form-group" style={{ marginBottom: '1rem' }}>
                                    <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1' }}>Mode</label>
                                    <select
                                        value={inviteMode}
                                        onChange={(e) => setInviteMode(e.target.value)}
                                        style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                    >
                                        <option value="auto">Auto (add if user exists, else invite)</option>
                                        <option value="existing">Add existing ParkEase user only</option>
                                        <option value="invite">Create invitation only</option>
                                    </select>
                                </div>
                                <div className="form-group" style={{ marginBottom: '1rem' }}>
                                    <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1' }}>Email Address</label>
                                    <input
                                        type="email"
                                        required
                                        value={inviteEmail}
                                        onChange={(e) => setInviteEmail(e.target.value)}
                                        style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                        placeholder="employee@company.com"
                                    />
                                </div>
                                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginBottom: '1rem' }}>
                                    <div className="form-group">
                                        <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1' }}>Role</label>
                                        <select
                                            value={inviteRole}
                                            onChange={(e) => setInviteRole(e.target.value)}
                                            style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                        >
                                            <option value={1}>Employee</option>
                                            <option value={0}>Admin</option>
                                        </select>
                                    </div>
                                    <div className="form-group">
                                        <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1' }}>Priority</label>
                                        <input
                                            type="number"
                                            min={1}
                                            max={10}
                                            value={invitePriority}
                                            onChange={(e) => setInvitePriority(e.target.value)}
                                            style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                            disabled={inviteMode === 'invite'}
                                            title={inviteMode === 'invite' ? 'Priority is set when the invite is accepted (default 1)' : ''}
                                        />
                                    </div>
                                </div>
                                <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                                    <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1' }}>Employee code (optional)</label>
                                    <input
                                        type="text"
                                        value={inviteEmployeeCode}
                                        onChange={(e) => setInviteEmployeeCode(e.target.value)}
                                        style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                        disabled={inviteMode === 'invite'}
                                        placeholder="E-1001"
                                    />
                                </div>
                                <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end' }}>
                                    <button
                                        type="button"
                                        onClick={() => { setShowInviteModal(false); resetInviteForm(); }}
                                        className="btn btn-secondary"
                                    >
                                        Cancel
                                    </button>
                                    <button type="submit" className="btn btn-primary" disabled={inviting}>
                                        {inviting ? 'Working…' : inviteMode === 'existing' ? 'Add member' : 'Continue'}
                                    </button>
                                </div>
                            </form>
                        )}
                    </div>
                </div>
            )}

            {/* Edit member modal */}
            {editMember && (
                <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.7)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <div style={{ background: '#1e293b', width: '100%', maxWidth: '440px', borderRadius: '12px', padding: '2rem', border: '1px solid rgba(255,255,255,0.1)' }}>
                        <h2 style={{ marginBottom: '0.35rem', color: 'white' }}>Edit Member</h2>
                        <p style={{ color: '#94a3b8', fontSize: '0.85rem', marginBottom: '1.5rem' }}>
                            {editMember.userName} · {editMember.userEmail}
                        </p>
                        <form onSubmit={handleUpdateMember}>
                            <div className="form-group" style={{ marginBottom: '1rem' }}>
                                <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1' }}>Role</label>
                                <select
                                    value={editMember.role}
                                    onChange={(e) => setEditMember({ ...editMember, role: e.target.value })}
                                    style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                >
                                    <option value={1}>Employee</option>
                                    <option value={0}>Admin</option>
                                </select>
                            </div>
                            <div className="form-group" style={{ marginBottom: '1rem' }}>
                                <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1' }}>Priority (1–10)</label>
                                <input
                                    type="number"
                                    min={1}
                                    max={10}
                                    required
                                    value={editMember.priority}
                                    onChange={(e) => setEditMember({ ...editMember, priority: e.target.value })}
                                    style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                />
                                <small style={{ color: '#64748b', display: 'block', marginTop: '4px' }}>
                                    Higher priority is preferred for waitlist ordering and booking policy thresholds.
                                </small>
                            </div>
                            <div className="form-group" style={{ marginBottom: '1.5rem' }}>
                                <label style={{ display: 'block', marginBottom: '8px', color: '#cbd5e1' }}>Employee Code</label>
                                <input
                                    type="text"
                                    maxLength={50}
                                    value={editMember.employeeCode}
                                    onChange={(e) => setEditMember({ ...editMember, employeeCode: e.target.value })}
                                    placeholder="Optional"
                                    style={{ width: '100%', padding: '10px', background: '#0f172a', border: '1px solid #334155', borderRadius: '6px', color: 'white' }}
                                />
                            </div>
                            <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end' }}>
                                <button type="button" onClick={() => setEditMember(null)} className="btn btn-secondary">Cancel</button>
                                <button type="submit" className="btn btn-primary" disabled={savingMember}>
                                    {savingMember ? 'Saving...' : 'Save Changes'}
                                </button>
                            </div>
                        </form>
                    </div>
                </div>
            )}
        </div>
    );
};

export default CompanyMembers;
