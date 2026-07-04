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
    
    // Modal state
    const [showInviteModal, setShowInviteModal] = useState(false);
    const [inviteEmail, setInviteEmail] = useState('');
    const [inviteRole, setInviteRole] = useState(1); // 1 = Employee
    const [inviting, setInviting] = useState(false);

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
                toast.error(response.message || "Failed to load members");
            }
        } catch (error) {
            toast.error("Could not reach server");
        } finally {
            setLoading(false);
        }
    };

    const handleInvite = async (e) => {
        e.preventDefault();
        setInviting(true);
        try {
            const response = await corporateService.inviteMember({ email: inviteEmail, role: parseInt(inviteRole) });
            if (response.success) {
                toast.success('Invitation sent successfully!');
                setShowInviteModal(false);
                setInviteEmail('');
            } else {
                toast.error(response.message || 'Failed to send invitation');
            }
        } catch (error) {
            toast.error('An error occurred during invitation');
        } finally {
            setInviting(false);
        }
    };

    const handleRemove = async (membershipId) => {
        if (!window.confirm("Are you sure you want to remove this member?")) return;
        
        try {
            const response = await corporateService.removeMember(membershipId);
            if (response.success) {
                toast.success('Member removed');
                loadMembers();
            } else {
                toast.error(response.message || 'Failed to remove member');
            }
        } catch (error) {
            toast.error('An error occurred during removal');
        }
    };

    if (!isCorporateMode) return null;

    return (
        <div className="container" style={{ padding: '2rem 0', color: '#f1f5f9' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem' }}>
                <h1 style={{ color: 'white', display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <span style={{ fontSize: '2rem' }}>👥</span> Company Members
                </h1>
                <button
                    onClick={() => setShowInviteModal(true)}
                    className="btn btn-primary"
                    style={{ padding: '0.6rem 1.2rem', display: 'flex', alignItems: 'center', gap: '8px' }}
                >
                    <span>✉️</span> Invite Member
                </button>
            </div>

            <div style={{ background: '#1e293b', borderRadius: '12px', border: '1px solid rgba(255,255,255,0.05)', overflow: 'hidden' }}>
                {loading ? (
                    <div style={{ padding: '3rem', textAlign: 'center' }}><div className="spinner"></div></div>
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
                            {members.map(member => (
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
                                        <button 
                                            onClick={() => handleRemove(member.id)}
                                            style={{ background: 'transparent', border: '1px solid rgba(239,68,68,0.3)', color: '#ef4444', padding: '6px 12px', borderRadius: '6px', cursor: 'pointer', transition: 'all 0.2s' }}
                                            onMouseEnter={e => { e.currentTarget.style.background = 'rgba(239,68,68,0.1)'; e.currentTarget.style.borderColor = '#ef4444' }}
                                            onMouseLeave={e => { e.currentTarget.style.background = 'transparent'; e.currentTarget.style.borderColor = 'rgba(239,68,68,0.3)' }}
                                        >
                                            Remove
                                        </button>
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

            {/* Invite Modal */}
            {showInviteModal && (
                <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.7)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <div style={{ background: '#1e293b', width: '100%', maxWidth: '400px', borderRadius: '12px', padding: '2rem', border: '1px solid rgba(255,255,255,0.1)' }}>
                        <h2 style={{ marginBottom: '1.5rem', color: 'white' }}>Invite Member</h2>
                        <form onSubmit={handleInvite}>
                            <div className="form-group" style={{ marginBottom: '1.5rem' }}>
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
                            <div className="form-group" style={{ marginBottom: '2rem' }}>
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
                            <div style={{ display: 'flex', gap: '1rem', justifyContent: 'flex-end' }}>
                                <button type="button" onClick={() => setShowInviteModal(false)} className="btn btn-secondary">Cancel</button>
                                <button type="submit" className="btn btn-primary" disabled={inviting}>
                                    {inviting ? 'Sending...' : 'Send Invite'}
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
