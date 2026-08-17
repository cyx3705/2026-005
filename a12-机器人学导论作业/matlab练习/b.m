% 单关节五次多项式轨迹 情况b
clear; clc; close all;

% 已知参数
theta_s = 120;  % 起始角度(deg)
theta_f = 60;   % 终止角度(deg)
tf = 1;         % 总时间(s)

% 五次多项式系数求解
a0 = theta_s;
a1 = 0;
a2 = 0;
% 五次多项式通用系数公式
a3 = 10*(theta_f-theta_s)/tf^3;
a4 = 15*(theta_s-theta_f)/tf^4;
a5 = 6*(theta_f-theta_s)/tf^5;

fprintf('=====五次多项式系数=====\n');
fprintf('a0=%.2f, a1=%.2f, a2=%.2f, a3=%.2f, a4=%.2f, a5=%.2f\n',a0,a1,a2,a3,a4,a5);

% 时间采样
t = linspace(0, tf, 100);
% 各阶轨迹计算
theta = a0 + a1*t + a2*t.^2 + a3*t.^3 + a4*t.^4 + a5*t.^5;
dtheta = a1 + 2*a2*t + 3*a3*t.^2 + 4*a4*t.^3 + 5*a5*t.^4;
ddtheta = 2*a2 + 6*a3*t + 12*a4*t.^2 + 20*a5*t.^3;
dddtheta = 6*a3 + 24*a4*t + 60*a5*t.^2;

% 打印函数表达式
fprintf('\n=====五次多项式各阶函数=====\n');
fprintf('关节角 θ(t) = %.0f + %.0f t + %.0f t² + %.0f t³ + %.0f t⁴ + %.0f t⁵ °\n',a0,a1,a2,a3,a4,a5);
fprintf('角速度 θ̇(t) = %.0f + %.0f t + %.0f t² + %.0f t³ + %.0f t⁴ °/s\n',a1,2*a2,3*a3,4*a4,5*a5);
fprintf('角加速度 θ̈(t) = %.0f + %.0f t + %.0f t² + %.0f t³ °/s²\n',2*a2,6*a3,12*a4,20*a5);
fprintf('加加速度 θ̈̇(t) = %.0f + %.0f t + %.0f t² °/s³\n',6*a3,24*a4,60*a5);

% 竖直分4张子图绘图
figure('Color','w');
subplot(4,1,1);
plot(t,theta,'LineWidth',1.5);
ylabel('关节角 \theta (^\circ)');
title('五次多项式轨迹：角度、角速度、角加速度、加加速度');
grid on;

subplot(4,1,2);
plot(t,dtheta,'LineWidth',1.5,'Color','#d95319');
ylabel('角速度 \dot{\theta} (^\circ/s)');
grid on;

subplot(4,1,3);
plot(t,ddtheta,'LineWidth',1.5,'Color','#0072bd');
ylabel('角加速度 \ddot{\theta} (^\circ/s^2)');
grid on;

subplot(4,1,4);
plot(t,dddtheta,'LineWidth',1.5,'Color','#edb120');
xlabel('时间 t (s)');
ylabel('加加速度 \dddot{\theta} (^\circ/s^3)');
grid on;
