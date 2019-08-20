using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PlayerInterface;

namespace ControllerLib
{
    struct Pt
    {
        public int x;
        public int y;
        public Pt(int _x, int _y)
        {
            x = _x;
            y = _y;
        }
        public static Pt start = new Pt(-10, -10);
        public static float Len(Pt a, Pt b)
        {
            return (float)Math.Pow((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y), 0.5);
        }


    }

    class Player
    {

        /// <summary>
        /// 42->50 +6/次   需要2次
        /// </summary>
        public int pos_update = 666;
        public Pt last_pos = Pt.start;
        public Pt pos;
        public bool dead = false;
        public int hp = 100;
        public int score = 0;
        public float shoot_cd = 0;
        public float bomb_cd = 0;

    }






    class Controller : PlayerInterface.IControl
    {
        public string GetTeamName()
        {
            return "蓝翔电焊专业一班";
        }

        /// <summary>
        /// 地图信息    -1圈外 0可用 1可炸 2墙 3有炸弹
        /// </summary>
        int[,] map = new int[14, 14];
        //更新地图
        void RefreshMap()
        {
            for (int i = 0; i < 14; i++)
            {
                for (int j = 0; j < 14; j++)
                {
                    map[i, j] = (int)go.GetMapType(i, j);
                }
            }
        }



        void RefreshInfo()
        {
            RefreshMap();
            //自己信息更新
            me.pos = new Pt(go.GetPosition()[0], go.GetPosition()[1]);
            willness[me.pos.x, me.pos.y] = 0;
            me.score = go.GetScore();
            me.hp = go.GetHP();


            //他人信息更新
            int theIndex = -1;
            for (int i = 1; i < 5; i++)
            {
                if (i != go.GetIndex())
                {
                    theIndex++;
                    Player x = others[theIndex];
                    x.pos = new Pt(go.PlayerPosition(i)[0], go.PlayerPosition(i)[1]);
                    x.hp = (int)go.PlayerHealth(i);
                    if (x.hp <= 0)
                        x.dead = true;
                    x.score = (int)go.PlayerScore(i);

                    x.bomb_cd = (int)go.PlayerBombCD(i);
                    x.shoot_cd = (int)go.PlayerShootCD(i);

                    //预判位置更新
                    if ((x.last_pos.x != x.pos.x || x.last_pos.y != x.pos.y) && x.pos_update++ >= 2)
                    {
                        x.pos_update = 0;
                        x.last_pos = x.pos;
                    }

                }
                else
                {
                    me.bomb_cd = (int)go.PlayerBombCD(i);
                    me.shoot_cd = (int)go.PlayerShootCD(i);
                }
            }


        }


        int disgust = 0;


        float nearst_enemy_len = 9999;
        /// <summary>
        /// 人工智能的崛起，根据环境只适应变化，并主动迭代升级
        /// </summary>
        void RefreshSetting()
        {
            Player aim = GetNearstPlayer();
            nearst_enemy_len = Pt.Len(aim.pos, me.pos);
            //1000为平常
            if (nearst_enemy_len < 8)
                enemy_bonus = 500 + (8 - (int)nearst_enemy_len) * 250;   //max:2500
            if (GetBoxCount() < 7 || go.GetRemainingTime() < 30)
            {
                //enemy_bonus += (10 - GetBoxCount()) * 200;  //max:2000
                if (GetBoxCount() < 10)
                    bonus_box = 1500 + (10 - GetBoxCount()) * 50;

            }


            if (others.Exists(x => x.hp - me.hp >= 20) && me.hp < 50)
                enemy_bonus += -1000 - (50 - me.hp) * 60;  //max:-4000

            //保重身体
            if (me.hp < 20 && go.GetRemainingTime() > 10 && GetBoxCount() > 4)
                enemy_bonus += -2000;


            if (me.shoot_cd > 0)
                enemy_bonus += -(int)(me.shoot_cd * 500);
            if (me.bomb_cd == 0)
                enemy_bonus += -2000;



            //移动消耗动态变化
            if (go.CanBomb())
                move_bonus = -250;
            else
                move_bonus = -200;

            //不要挨近我！
            if (disgust > 4000)
                disgust = 4000;
            if (disgust > 0)
                disgust -= 15;
            if (disgust < 0)
                disgust = 0;
            //1s---->+1500
            if (nearst_enemy_len < 1)
                disgust += 45;

            enemy_bonus -= disgust;


        }



        //找到最近的玩家
        Player GetNearstPlayer()
        {
            Player best = null;
            float len = 999999;

            others.ForEach(x =>
            {
                if (!x.dead && Pt.Len(x.pos, me.pos) < len)
                {
                    best = x;
                    len = Pt.Len(x.pos, me.pos);
                }
            });
            return best;
        }




        bool runOnced = false;
        void RunOnce()
        {
            if (runOnced)
                return;
            runOnced = true;

            others.Add(new Player());
            others.Add(new Player());
            others.Add(new Player());

        }




        List<Player> others = new List<Player>();
        //自己
        Player me = new Player();

        //简化
        IEntity go;

        public void Update(IEntity entity)
        {
            //if (stopme)
            //return;
            //stopme = true;
            // 简化代码
            go = entity;

            //第一次使用
            RunOnce();

            //更新信息
            RefreshInfo();

            //自我迭代
            RefreshSetting();

            //当当当当！核武器！启动！
            RunAI();


            return;
        }



        void RunAI()
        {

            if (!go.IsMoving())
            {
                if (go.CanShoot())
                {
                    TryShoot();
                }

                if (!TryMove())
                {
                    if (go.CanBomb())
                        go.SetBomb();
                }
            }

            if (go.CanUpgrade())
                TryUseSkill();



        }

        bool TryMove()
        {
            CalMap();
            List<Pt> pts = GetBestWay();

            if (pts.Count < 2)
                return false;
            Pt move = pts[pts.Count - 2];
            if (move.x > me.pos.x)
                go.MoveEast();
            else if (move.x < me.pos.x)
                go.MoveWest();
            else if (move.y < me.pos.y)
                go.MoveSouth();
            else if (move.y > me.pos.y)
                go.MoveNorth();
            else return false;
            return true;

        }

        bool TryShoot()
        {
            foreach (var x in others)
            {
                if (x.dead)
                    continue;

                if (x.pos.x == me.pos.x && x.last_pos.x == me.pos.x)
                {
                    if (me.pos.y > x.pos.y && IsPassiable(me.pos, x.pos))
                    {
                        go.TurnSouth();
                        go.Shoot();
                        return true;
                    }
                    else if (me.pos.y < x.pos.y && IsPassiable(me.pos, x.pos))
                    {
                        go.TurnNorth();
                        go.Shoot();
                        return true;
                    }
                }
                else if (x.pos.y == me.pos.y && x.last_pos.y == me.pos.y)
                {
                    if (me.pos.x > x.pos.x && IsPassiable(me.pos, x.pos))
                    {
                        go.TurnWest();
                        go.Shoot();
                        return true;
                    }
                    else if (me.pos.x < x.pos.x && IsPassiable(me.pos, x.pos))
                    {
                        go.TurnEast();
                        go.Shoot();
                        return true;
                    }


                }
            }

            return false;

        }


        /// <summary>
        /// 目前只写了加血，因为感觉其他用处不大！
        /// </summary>
        /// <returns></returns>
        bool TryUseSkill()
        {
            if (me.score >= 30)
            {
                if (me.hp <= 70 || go.GetRemainingTime() < 0.5f)
                {
                    go.BuffHP();
                    return true;

                }
            }


            return false;

        }



        /// <summary>
        /// 是否直线连同
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        bool IsPassiable(Pt from, Pt to)
        {
            if (from.y == to.y)
            {
                if (from.x > to.x)
                {
                    Pt temp = from;
                    from = to;
                    to = temp;
                }

                for (int i = from.x + 1; i < to.x; i++)
                {
                    if (!MapAvailable(new Pt(i, from.y)) || (GetMap(new Pt(i, from.y)) != 0 && GetMap(new Pt(i, from.y)) != 3))
                        return false;
                }
                return true;
            }
            else if (from.x == to.x)
            {
                if (from.y > to.y)
                {
                    Pt temp = from;
                    from = to;
                    to = temp;
                }

                for (int i = from.y + 1; i < to.y; i++)
                {
                    if (!MapAvailable(new Pt(from.x, i)) || GetMap(new Pt(from.x, i)) != 0)
                        return false;
                }
                return true;
            }
            return false;

        }



        #region 地图信息以及权值计算



        int GetBoxCount()
        {
            int count = 0;
            for (int i = 0; i < cmap.GetLength(0); i++)
            {
                for (int j = 0; j < cmap.GetLength(1); j++)
                {
                    if (map[i, j] == 1)
                        count++;
                }
            }
            return count;

        }




        /// <summary>
        /// 意愿：AI的喜爱倾向->解决犹豫不决问题
        /// </summary>
        int[,] willness = new int[14, 14];
        /// <summary>
        /// 每个坐标的推荐度
        /// </summary>
        int[,] cmap = new int[14, 14];

        /// <summary>
        /// 不可继承加成（最终格加成）（如果加在这上面这不能计算路径）
        /// </summary>
        int[,] final_addition = new int[14, 14];


        /// <summary>
        /// 不可继承加成（计算路径）
        /// </summary>
        int[,] map_addition = new int[14, 14];

        /// <summary>
        /// 最高紧急通杀令(紧急加成）
        /// </summary>
        int[,] final_DangerAddition = new int[14, 14];

        void AddFinal_Addition(Pt pt, int num) { final_addition[pt.x, pt.y] += num; }



        /// <summary>
        /// 图论连接元素
        /// </summary>
        Pt[,] fromMap = new Pt[14, 14];

        bool MapAvailable(Pt pt)
        {
            if (pt.x < 0 || pt.x > 13 || pt.y < 0 || pt.y > 13)
                return false;
            return true;
        }
        int GetMap(Pt pt) { return map[pt.x, pt.y]; }
        void SetFromMap(Pt pt, Pt from) { fromMap[pt.x, pt.y] = from; }
        void SetCmap(Pt pt, int num) { cmap[pt.x, pt.y] = num; }


        List<Pt> allWays = new List<Pt>();
        /// <summary>
        /// 第一次则返回true
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="from"></param>
        /// <param name="add"></param>
        /// <returns></returns>
        bool NewComing(Pt pt, Pt from, int add)
        {
            if (cmap[pt.x, pt.y] == MIN_VALVE)
            {
                //防止炸弹感染自己位置
                if (from.x == me.pos.x && from.y == me.pos.y)
                {
                    SetCmap(pt, GetCmap(from) + add);
                    //不要问我其他，我只能说修复bug贼难受（这里是防止过度青睐炸弹！）
                    AddFinal_Addition(pt, map_addition[from.x, from.y]);
                }
                else
                    SetCmap(pt, GetCmap(from) + add + map_addition[from.x, from.y]);
                SetFromMap(pt, from);
                allWays.Add(pt);
                return true;
            }

            //更新指向（最短路径）
            if (GetCmap(pt) < GetCmap(from) + add + map_addition[from.x, from.y])
            {
                SetCmap(pt, GetCmap(from) + add + map_addition[from.x, from.y]);
                SetFromMap(pt, from);
                return true;
            }
            return false;
        }


        int GetCmap(Pt pt) { return cmap[pt.x, pt.y]; }


        bool[,] mapEffected = new bool[14, 14];
        void SetBoxEffect(Pt a, int add, bool force = false, bool remember = true)
        {
            if (mapEffected[a.x, a.y] && !force)
                return;
            if (remember)
                mapEffected[a.x, a.y] = true;
            Pt pt = a;
            pt.x += 1;
            if (MapAvailable(pt))
                AddFinal_Addition(pt, add);
            pt.x -= 2;
            if (MapAvailable(pt))
                AddFinal_Addition(pt, add);
            pt.x += 1;
            pt.y += 1;
            if (MapAvailable(pt))
                AddFinal_Addition(pt, add);
            pt.y -= 2;
            if (MapAvailable(pt))
                AddFinal_Addition(pt, add);
        }


        bool IsAnyBoomerHere()
        {
            return others.Exists(x => Pt.Len(x.pos, me.pos) <= 2 && x.bomb_cd > 0);
        }

        void CheckNoWayDanger(Pt a, int add)
        {
            if (mapEffected[a.x, a.y])
                return;
            int count = 0;
            mapEffected[a.x, a.y] = true;
            Pt pt = a;
            pt.x += 1;
            if (MapAvailable(pt))
            {
                if (GetMap(pt) != 0)
                    count++;
            }
            else count++;

            pt.x -= 2;
            if (MapAvailable(pt))
            {
                if (GetMap(pt) != 0)
                    count++;
            }
            else count++;
            pt.x += 1;
            pt.y += 1;
            if (MapAvailable(pt))
            {
                if (GetMap(pt) != 0)
                    count++;
            }
            else count++;
            pt.y -= 2;
            if (MapAvailable(pt))
            {
                if (GetMap(pt) != 0)
                    count++;
            }
            else count++;

            pt.y += 1;
            if (count >= 3)
            {
                final_DangerAddition[pt.x, pt.y] += add;
            }



        }






        void SetBombEffect(Pt a, int add)
        {
            if (mapEffected[a.x, a.y])
                return;
            mapEffected[a.x, a.y] = true;
            Pt pt = a;
            if (MapAvailable(pt))
            {
                AddFinal_Addition(pt, 4 * add);
            }

            pt.x += 1;
            if (MapAvailable(pt))
            {
                AddFinal_Addition(pt, 3 * add);
                //将炸弹附近的箱子视为空白
                if (GetMap(pt) == 1)
                {
                    if (mapEffected[pt.x, pt.y])
                        SetBoxEffect(pt, -bonus_box, true);
                    else
                        mapEffected[pt.x, pt.y] = true;
                }
            }
            pt.x -= 2;
            if (MapAvailable(pt))
            {
                AddFinal_Addition(pt, 3 * add);
                if (GetMap(pt) == 1)
                {
                    if (mapEffected[pt.x, pt.y])
                        SetBoxEffect(pt, -bonus_box, true);
                    else
                        mapEffected[pt.x, pt.y] = true;
                }
            }
            pt.x += 1;
            pt.y += 1;
            if (MapAvailable(pt))
            {
                AddFinal_Addition(pt, 3 * add);
                if (GetMap(pt) == 1)
                {
                    if (mapEffected[pt.x, pt.y])
                        SetBoxEffect(pt, -bonus_box, true);
                    else
                        mapEffected[pt.x, pt.y] = true;
                }
            }
            pt.y -= 2;
            if (MapAvailable(pt))
            {
                AddFinal_Addition(pt, 3 * add);
                if (GetMap(pt) == 1)
                {
                    if (mapEffected[pt.x, pt.y])
                        SetBoxEffect(pt, -bonus_box, true);
                    else
                        mapEffected[pt.x, pt.y] = true;
                }
            }

            //斜角
            pt.x += 1;
            if (MapAvailable(pt))
                AddFinal_Addition(pt, add / 10);
            pt.x -= 2;
            if (MapAvailable(pt))
                AddFinal_Addition(pt, add / 10);
            pt.y += 2;
            if (MapAvailable(pt))
                AddFinal_Addition(pt, add / 10);
            pt.x += 2;
            if (MapAvailable(pt))
                AddFinal_Addition(pt, add / 10);


        }

        void SetBombEffect2(Pt a, int add)
        {
            if (mapEffected[a.x, a.y])
                return;
            mapEffected[a.x, a.y] = true;
            Pt pt = a;
            if (MapAvailable(pt))
            {
                map_addition[pt.x, pt.y] += 4 * add;
                AddFinal_Addition(pt, 4 * add);
            }

            pt.x += 1;
            if (MapAvailable(pt))
            {
                map_addition[pt.x, pt.y] += 4 * add;
                AddFinal_Addition(pt, 4 * add);
                //将炸弹附近的箱子视为空白
                if (GetMap(pt) == 1)
                {
                    if (mapEffected[pt.x, pt.y])
                        SetBoxEffect(pt, -bonus_box, true);
                    else
                        mapEffected[pt.x, pt.y] = true;
                }
            }
            pt.x -= 2;
            if (MapAvailable(pt))
            {
                map_addition[pt.x, pt.y] += 4 * add;
                AddFinal_Addition(pt, 4 * add);
                if (GetMap(pt) == 1)
                {
                    if (mapEffected[pt.x, pt.y])
                        SetBoxEffect(pt, -bonus_box, true);
                    else
                        mapEffected[pt.x, pt.y] = true;
                }
            }
            pt.x += 1;
            pt.y += 1;
            if (MapAvailable(pt))
            {
                map_addition[pt.x, pt.y] += 4 * add;
                AddFinal_Addition(pt, 4 * add);
                if (GetMap(pt) == 1)
                {
                    if (mapEffected[pt.x, pt.y])
                        SetBoxEffect(pt, -bonus_box, true);
                    else
                        mapEffected[pt.x, pt.y] = true;
                }
            }
            pt.y -= 2;
            if (MapAvailable(pt))
            {
                map_addition[pt.x, pt.y] += 4 * add;
                AddFinal_Addition(pt, 4 * add);
                if (GetMap(pt) == 1)
                {
                    if (mapEffected[pt.x, pt.y])
                        SetBoxEffect(pt, -bonus_box, true);
                    else
                        mapEffected[pt.x, pt.y] = true;
                }
            }

            ////斜角
            //pt.x += 1;
            //if (MapAvailable(pt))
            //{
            //    map_addition[pt.x, pt.y] += add / 10;
            //    AddFinal_Addition(pt, add / 10);
            //}
            //pt.x -= 2;
            //if (MapAvailable(pt))
            //{
            //    map_addition[pt.x, pt.y] += add / 10;
            //    AddFinal_Addition(pt, add / 10);
            //}
            //pt.y += 2;
            //if (MapAvailable(pt))
            //{
            //    map_addition[pt.x, pt.y] += add / 10;
            //    AddFinal_Addition(pt, add / 10);
            //}
            //pt.x += 2;
            //if (MapAvailable(pt))
            //{
            //    map_addition[pt.x, pt.y] += add / 10;
            //    AddFinal_Addition(pt, add / 10);
            //}


        }


        //目标：初始炸弹位置选择

        ////////.///////////////////
        //平格移动消耗：-100~-200
        //箱子加成：1500=x
        //炸弹：-4x, 斜角-x/10 中间 -4x 走上去消耗 x/2
        //缩圈威胁：-50000
        //敌人加成：1000
        //
        //
        ///////////////////////////

        int move_bonus = -100;
        int bonus_box = 1500;
        void CalMap(Pt pt, Pt from)
        {
            if (!MapAvailable(pt))
                return;

            // 地图信息    -1圈外 0可用 1可炸 2墙 3有炸弹
            switch (GetMap(pt))
            {
                case 0:

                    if (!NewComing(pt, from, move_bonus))
                        return;

                    //查看走这步是不是个坑
                    if (nearst_enemy_len < 3)
                        CheckNoWayDanger(pt, (int)(-(4 * bonus_box)));

                    //继续走
                    CalMap(new Pt(pt.x - 1, pt.y), pt);
                    CalMap(new Pt(pt.x + 1, pt.y), pt);
                    CalMap(new Pt(pt.x, pt.y + 1), pt);
                    CalMap(new Pt(pt.x, pt.y - 1), pt);

                    break;
                case 1:
                    SetBoxEffect(pt, bonus_box);
                    break;
                case 2:
                    break;
                case 3:
                    //SetBombEffect(pt, -bonus_box);
                    break;

            }



        }




        void CentralBonus()
        {
            for (int i = 0; i < cmap.GetLength(0); i++)
            {
                for (int j = 0; j < cmap.GetLength(1); j++)
                {
                    int far = Math.Abs(i - 7) + Math.Abs(j - 7);
                    AddFinal_Addition(new Pt(i, j), (14 - far) * 100);
                }
            }
        }



        void CalCircleDanger()
        {
            float time = go.GetRemainingTime();
            if (time > 40)
                return;
            int circle = go.GetCircle();

            if (circle == 5)
                circle = 4;

            for (int i = 0; i < cmap.GetLength(0); i++)
            {
                for (int j = 0; j < cmap.GetLength(1); j++)
                {
                    if (i <= circle || i >= 13 - circle || j <= circle || j >= 13 - circle)
                    {

                        map_addition[i, j] += -50000;
                        AddFinal_Addition(new Pt(i, j), -50000);
                        if (me.pos.x == i && me.pos.y == j)
                            enemy_bonus = -5000;
                    }

                }
            }
        }

        int enemy_bonus = 1000;
        void EnemyBonus()
        {
            others.ForEach(x =>
            {
                if (!x.dead && MapAvailable(x.pos) && GetCmap(x.pos) != MIN_VALVE)
                {
                    int bonus = enemy_bonus;
                    if (x.hp <= 40)
                        bonus += 2500;
                    else if (x.hp <= 60)
                        bonus += 1000;
                    else if (x.hp <= 80)
                        bonus += 500;


                    if (x.shoot_cd != 0)
                        bonus += (int)(300 * x.shoot_cd + 100 * x.bomb_cd);//max: 1200+300

                    if (bonus < 0)
                        return;
                    SetBoxEffect(x.pos, bonus, true, false);
                }
            });

        }


        /// <summary>
        /// 避免自残
        /// </summary>
        void AvoidSuicide()
        {
            if (allWays.Count == 3)
            {
                allWays.ForEach(x =>
                {
                    Pt pt = x;
                    int count = 0;
                    pt.x += 1;
                    if (MapAvailable(pt) && GetMap(pt) == 0)
                        count++;
                    pt.x -= 2;
                    if (MapAvailable(pt) && GetMap(pt) == 0)
                        count++;
                    pt.x += 1;
                    pt.y += 1;
                    if (MapAvailable(pt) && GetMap(pt) == 0)
                        count++;
                    pt.y -= 2;
                    if (MapAvailable(pt) && GetMap(pt) == 0)
                        count++;

                    pt.y += 1;
                    if (count >= 2)
                        AddFinal_Addition(pt, -bonus_box * 4);
                });
            }
            else if (allWays.Count == 4)
            {
                allWays.ForEach(x =>
                {
                    Pt pt = x;
                    int count = 0;
                    pt.x += 1;
                    if (MapAvailable(pt) && GetMap(pt) == 0)
                        count++;
                    pt.x -= 2;
                    if (MapAvailable(pt) && GetMap(pt) == 0)
                        count++;
                    pt.x += 1;
                    pt.y += 1;
                    if (MapAvailable(pt) && GetMap(pt) == 0)
                        count++;
                    pt.y -= 2;
                    if (MapAvailable(pt) && GetMap(pt) == 0)
                        count++;

                    pt.y += 1;
                    if (count >= 3)
                        AddFinal_Addition(pt, -bonus_box * 4);
                });
            }


        }



        Random r = new Random();
        const int MIN_VALVE = -999999;
        /// <summary>
        /// 地图数据计算，加权
        /// </summary>
        void CalMap()
        {
            allWays.Clear();
            allWays.Add(me.pos);
            for (int i = 0; i < cmap.GetLength(0); i++)
            {
                for (int j = 0; j < cmap.GetLength(1); j++)
                {
                    cmap[i, j] = MIN_VALVE;
                    fromMap[i, j] = Pt.start;
                    final_addition[i, j] = 0;
                    final_DangerAddition[i, j] = 0;
                    map_addition[i, j] = 0;
                    mapEffected[i, j] = false;
                }
            }




            SetCmap(me.pos, 0);



            for (int i = 0; i < cmap.GetLength(0); i++)
            {
                for (int j = 0; j < cmap.GetLength(1); j++)
                {
                    //新版炸弹
                    if (GetMap(new Pt(i, j)) == 3)
                    {
                        SetBombEffect2(new Pt(i, j), -bonus_box);
                    }

                }
            }

            //缩圈前影响
            CalCircleDanger();

            CalMap(new Pt(me.pos.x - 1, me.pos.y), me.pos);
            CalMap(new Pt(me.pos.x + 1, me.pos.y), me.pos);
            CalMap(new Pt(me.pos.x, me.pos.y + 1), me.pos);
            CalMap(new Pt(me.pos.x, me.pos.y - 1), me.pos);






            //炸敌人
            EnemyBonus();
            //防止开始让血
            AvoidSuicide();
            //中心加成
            CentralBonus();

        }


        /// <summary>
        /// 返回一系列点，最后一个点为第一步
        /// </summary>
        /// <returns></returns>
        List<Pt> GetBestWay()
        {
            List<Pt> pts = new List<Pt>();
            Pt best = Pt.start;
            int max = MIN_VALVE;

            for (int i = 0; i < cmap.GetLength(0); i++)
            {
                for (int j = 0; j < cmap.GetLength(1); j++)
                {
                    Print("地图", (cmap[i, j] + final_addition[i, j] + willness[i, j] + map_addition[i, j] + final_DangerAddition[i, j]).ToString() + " ");//cmap[i, j] + final_addition[i, j]

                    if (cmap[i, j] + final_addition[i, j] + willness[i, j] + map_addition[i, j] + final_DangerAddition[i, j] > max)
                    {
                        max = cmap[i, j] + final_addition[i, j] + willness[i, j] + map_addition[i, j] + final_DangerAddition[i, j];
                        best = new Pt(i, j);
                    }
                }
                Print("地图", "\n");
            }

            Print("地图", "\n");


            //这里是重点调试的数据！·····················注意！

            //if (willness[best.x, best.y] > 400)
            //    willness[best.x, best.y] = 0;
            //else
            //    willness[best.x, best.y] += r.Next(110,220);
            willness[best.x, best.y] = 110;


            while (true)
            {
                if (best.x == Pt.start.x && best.y == Pt.start.y)
                    break;
                pts.Add(best);
                Print("", best.x + "," + best.y + " ");
                best = fromMap[best.x, best.y];
            }

            Print("", "myPos:" + me.pos.x + "," + me.pos.y + "\t\ttime:" + go.GetRemainingTime() + "\n\n\n");


            return pts;

        }



        #endregion


        #region Debug

        //System.IO.StreamWriter sw = new System.IO.StreamWriter(@"C:\\debug.txt", false);
        void Print(string type, object ob)
        {
            //if (type == "地图")
            //sw.Write(ob);
            //    return;

            //sw.Write(ob);
        }






        #endregion







    }
}
