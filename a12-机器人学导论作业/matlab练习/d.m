clear; clc; close all;
t = 0:0.01:1;
theta_s = 120;
theta_f = 60;

%% ========== a题 三次位置 ==========
[q_a,qd_a,qdd_a] = jtraj(theta_s,theta_f,t);
q_a_manual = 120 - 180*t.^2 + 120*t.^3;

figure;
subplot(3,1,1);
plot(t,q_a,'r-','LineWidth',1.5); hold on;
plot(t,q_a_manual,'g--','LineWidth',1.5);
title('a题：三次多项式位置对比');
legend('jtraj工具箱','手动公式');
grid on;

%% ========== b题 五次速度 ==========
qd0=0; qdf=0; qdd0=0; qddf=0;
qd_b_manual = -900*t.^2 + 1800*t.^3 - 900*t.^4;

subplot(3,1,2);
plot(t,qd_b_manual,'r-','LineWidth',1.5);
title('b题：五次多项式角速度');
grid on;

%% ========== c题 五次加速度 ==========
qdd_b_manual = -1800*t + 5400*t.^2 - 3600*t.^3;

subplot(3,1,3);
plot(t,qdd_b_manual,'r-','LineWidth',1.5);
title('c题：五次多项式角加速度');
grid on;